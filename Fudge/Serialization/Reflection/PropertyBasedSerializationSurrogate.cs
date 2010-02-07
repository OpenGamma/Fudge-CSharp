﻿/* <!--
 * Copyright (C) 2009 - 2010 by OpenGamma Inc. and other contributors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 *     
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * -->
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace Fudge.Serialization.Reflection
{
    /// <summary>
    /// Used to handle classes which are bean-style (i.e. have matching getters and setters and a default constructor).
    /// </summary>
    /// <remarks>
    /// For lists (i.e. properties that implement <see cref="IList{T}"/>, no setter is required but the object itself must construct the list
    /// so that values can be added to it.
    /// </remarks>
    public class PropertyBasedSerializationSurrogate : IFudgeSerializationSurrogate
    {
        private readonly FudgeContext context;
        private readonly Type type;
        private readonly ConstructorInfo constructor;
        private readonly Dictionary<string, MorePropertyData> propMap = new Dictionary<string, MorePropertyData>();

        public PropertyBasedSerializationSurrogate(FudgeContext context, Type type)
            : this(context, TypeDataCache.FromContext(context).GetTypeData(type))
        {
        }

        internal PropertyBasedSerializationSurrogate(FudgeContext context, TypeData typeData)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (typeData == null)
                throw new ArgumentNullException("typeData");
            if (!CanHandle(typeData))
                throw new ArgumentOutOfRangeException("typeData", "PropertyBasedSerializationSurrogate cannot handle " + typeData.Type.FullName);

            this.context = context;
            this.type = typeData.Type;

            if (typeData.DefaultConstructor == null)
            {
                throw new FudgeRuntimeException("Type " + type.FullName + " cannot use reflection-based serialization as it does not have a public default constructor.");
            }
            this.constructor = typeData.DefaultConstructor;

            // Pull out all the properties
            foreach (var prop in typeData.Properties)
            {
                var propData = new MorePropertyData { PropertyData = prop };
                switch (prop.Kind)
                {
                    case TypeData.TypeKind.FudgePrimitive:
                        propData.Adder = this.PrimitiveAdd;
                        propData.Serializer = this.PrimitiveSerialize;
                        break;
                    case TypeData.TypeKind.List:
                        propData.Adder = CreateMethodDelegate<Action<MorePropertyData, object, IFudgeField, IFudgeDeserializer>>("ListAdd", prop.TypeData.SubType);
                        propData.Serializer = CreateMethodDelegate<Action<MorePropertyData, object, IFudgeSerializer>>("ListSerialize", prop.TypeData.SubType);
                        break;
                    case TypeData.TypeKind.Object:
                        propData.Adder = CreateMethodDelegate<Action<MorePropertyData, object, IFudgeField, IFudgeDeserializer>>("ObjectAdd", prop.Type);
                        propData.Serializer = this.ObjectSerialize;
                        break;
                    default:
                        throw new FudgeRuntimeException("Invalid property type in PropertyBasedSerializationSurrogate for " + type.FullName + "." + prop.Name);    // Should have been caught in CanHandle
                }
                propMap.Add(prop.SerializedName, propData);
            }
        }

        public static bool CanHandle(FudgeContext context, Type type)
        {
            return CanHandle(TypeDataCache.FromContext(context).GetTypeData(type));
        }

        internal static bool CanHandle(TypeData typeData)
        {
            if (typeData.DefaultConstructor == null)
                return false;
            foreach (var prop in typeData.Properties)
            {
                switch (prop.Kind)
                {
                    case TypeData.TypeKind.FudgePrimitive:
                    case TypeData.TypeKind.List:
                    case TypeData.TypeKind.Object:
                        // OK
                        break;
                    default:
                        // Unknown
                        return false;
                }

                if (!prop.HasPublicSetter && prop.Kind != TypeData.TypeKind.List)
                {
                    // Not bean-style
                    return false;
                }
            }
            return true;
        }

        #region IFudgeSerializationSurrogate Members

        /// <inheritdoc/>
        public void Serialize(object obj, IFudgeSerializer serializer)
        {
            foreach (var entry in propMap)
            {
                string name = entry.Key;
                MorePropertyData prop = entry.Value;
                object val = prop.PropertyData.Info.GetValue(obj, null);
                prop.Serializer(prop, val, serializer);
            }
        }

        /// <inheritdoc/>
        public object BeginDeserialize(IFudgeDeserializer deserializer, int dataVersion)
        {
            object newObj = constructor.Invoke(null);
            deserializer.Register(newObj);
            return newObj;
        }

        /// <inheritdoc/>
        public bool DeserializeField(IFudgeDeserializer deserializer, IFudgeField field, int dataVersion, object state)
        {
            MorePropertyData prop;
            if (propMap.TryGetValue(field.Name, out prop))
            {
                prop.Adder(prop, state, field, deserializer);
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public object EndDeserialize(IFudgeDeserializer deserializer, int dataVersion, object state)
        {
            return state;
        }

        #endregion

        #region Helper functions

        private T CreateMethodDelegate<T>(string name, Type valueType) where T : class
        {
            var method = this.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(new Type[] { valueType });
            return Delegate.CreateDelegate(typeof(T), this, method) as T;
        }

        private void PrimitiveSerialize(MorePropertyData prop, object val, IFudgeSerializer serializer)
        {
            serializer.Write(prop.PropertyData.SerializedName, val);
        }

        private void PrimitiveAdd(MorePropertyData prop, object obj, IFudgeField field, IFudgeDeserializer deserializer)
        {
            object val = context.TypeHandler.ConvertType(field.Value, prop.PropertyData.Type);
            prop.PropertyData.Info.SetValue(obj, val, null);
        }

        private void ListSerialize<T>(MorePropertyData prop, object val, IFudgeSerializer serializer)
        {
            var list = (IList<T>)val;
            foreach (T item in list)
            {
                switch(prop.PropertyData.TypeData.SubTypeData.Kind)
                {
                    case TypeData.TypeKind.FudgePrimitive:
                        PrimitiveSerialize(prop, item, serializer);
                        break;
                    case TypeData.TypeKind.List:
                        // TODO 2010-02-07 t0rx -- Serializing lists of lists
                        throw new FudgeRuntimeException("Serializing lists of lists is not yet supported.");
                        break;
                    case TypeData.TypeKind.Object:
                        ObjectSerialize(prop, item, serializer);
                        break;
                    default:
                        throw new FudgeRuntimeException("Unknown kind of type.");
                }
            }
        }

        private void ListAdd<T>(MorePropertyData prop, object obj, IFudgeField field, IFudgeDeserializer deserializer)
        {
            object val = null;
            switch (prop.PropertyData.TypeData.SubTypeData.Kind)
            {
                case TypeData.TypeKind.FudgePrimitive:
                    val = context.TypeHandler.ConvertType(field.Value, prop.PropertyData.TypeData.SubType);
                    break;
                case TypeData.TypeKind.List:
                    // TODO 2010-02-07 t0rx -- Deserializing lists of lists
                    throw new FudgeRuntimeException("Deserializing lists of lists is not yet supported.");
                case TypeData.TypeKind.Object:
                    val = deserializer.FromField<object>(field);
                    break;
                default:
                    throw new FudgeRuntimeException("Unknown kind of type.");
            }
            var list = (IList<T>)prop.PropertyData.Info.GetValue(obj, null);
            list.Add((T)val);
        }

        private void ObjectSerialize(MorePropertyData prop, object val, IFudgeSerializer serializer)
        {
            // We can't tell whether it might have cycles, so serialize as a reference
            serializer.WriteRef(prop.PropertyData.SerializedName, val);
        }

        private void ObjectAdd<T>(MorePropertyData prop, object obj, IFudgeField field, IFudgeDeserializer deserializer) where T : class
        {
            T subObject = deserializer.FromField<T>(field);
            prop.PropertyData.Info.SetValue(obj, subObject, null);
        }

        #endregion

        private class MorePropertyData
        {
            public TypeData.PropertyData PropertyData { get; set; }
            public Action<MorePropertyData, object, IFudgeSerializer> Serializer { get; set; }
            public Action<MorePropertyData, object, IFudgeField, IFudgeDeserializer> Adder { get; set; }
        }
    }
}
