/**
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
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Fudge.Serialization;
using System.IO;
using Fudge.Encodings;
using System.Diagnostics;
using Fudge.Types;

namespace Fudge.Tests.Unit.Serialization
{
    public class FudgeSerializerTest
    {
        private readonly FudgeContext context = new FudgeContext();

        [Fact]
        public void VerySimpleExample()
        {
            // This code is used as the example in the NamespaceDoc for Fudge.Serialization

            // Create a context and a serializer
            var context = new FudgeContext();
            var serializer = new FudgeSerializer(context);

            // Our object to serialize
            var temperatureRange = new TemperatureRange { High = 28.3, Low = 13.2, Average = 19.6 };

            // Serialize it to a MemoryStream
            var stream = new MemoryStream();
            var streamWriter = new FudgeEncodedStreamWriter(context, stream);
            serializer.Serialize(streamWriter, temperatureRange);

            // Reset the stream and deserialize a new object from it
            stream.Position = 0;
            var streamReader = new FudgeEncodedStreamReader(context, stream);
            var range2 = (TemperatureRange)serializer.Deserialize(streamReader);

            // Just check a value matches
            Assert.Equal(temperatureRange, range2);
        }


        [Fact]
        public void SimpleExampleWithSurrogate()
        {
            var typeMap = new SerializationTypeMap(context);
            typeMap.RegisterType(typeof(Explicit.Address), new Explicit.AddressSerializer());
            var serializer = new FudgeSerializer(context, typeMap);

            var address = new Explicit.Address("Our House", "In the middle of our street", "MD1");
            var msg = serializer.SerializeToMsg(address);

            var address2 = (Explicit.Address)serializer.Deserialize(msg);

            Assert.Equal(address.Line1, address2.Line1);
            Assert.Equal(address.Line2, address2.Line2);
            Assert.Equal(address.Zip, address2.Zip);
        }

        [Fact]
        public void SimpleExampleWithIFudgeSerializable()
        {
            var typeMap = new SerializationTypeMap(context);
            typeMap.RegisterType(typeof(Explicit.Tick));
            var serializer = new FudgeSerializer(context, typeMap);

            var tick = new Explicit.Tick { Ticker = "FOO", Bid = 12.3, Offer = 12.9 };
            var msg = serializer.SerializeToMsg(tick);

            var tick2 = (Explicit.Tick)serializer.Deserialize(msg);

            Assert.Equal(tick.Ticker, tick2.Ticker);
            Assert.Equal(tick.Bid, tick2.Bid);
            Assert.Equal(tick.Offer, tick2.Offer);
        }

        [Fact]
        public void InlineObject()
        {
            var typeMap = new SerializationTypeMap(context);
            typeMap.RegisterType(typeof(Explicit.Person));
            typeMap.RegisterType(typeof(Explicit.Address), new Explicit.AddressSerializer());
            var serializer = new FudgeSerializer(context, typeMap);

            var person = new Explicit.Person { Name = "Bob", MainAddress = new Explicit.Address("Foo", "Bar", null) };
            var msg = serializer.SerializeToMsg(person);

            var person2 = (Explicit.Person)serializer.Deserialize(msg);
            Assert.NotSame(person.MainAddress, person2.MainAddress);
            Assert.Equal(person.MainAddress.Line1, person2.MainAddress.Line1);
        }

        [Fact]
        public void ReferencedObject()
        {
            var typeMap = new SerializationTypeMap(context);
            typeMap.RegisterType(typeof(Explicit.Sibling));
            typeMap.RegisterType(typeof(Explicit.Address), new Explicit.AddressSerializer());
            var serializer = new FudgeSerializer(context, typeMap);

            var bob = new Explicit.Sibling { Name = "Bob" };
            var shirley = new Explicit.Sibling { Name = "Shirley" };
            bob.Siblings.Add(shirley);                          // We don't reciprocate yet as that would generate a cycle

            var msg = serializer.SerializeToMsg(bob);

            var bob2 = (Explicit.Sibling)serializer.Deserialize(msg);
            Assert.NotSame(bob, bob2);
            Assert.Equal(1, bob2.Siblings.Count);
            Assert.NotSame(shirley, bob2.Siblings[0]);
            Assert.Equal("Shirley", bob2.Siblings[0].Name);
        }

        [Fact]
        public void CircularReference()
        {
            var typeMap = new SerializationTypeMap(context);
            typeMap.RegisterType(typeof(Explicit.Sibling));
            typeMap.RegisterType(typeof(Explicit.Address), new Explicit.AddressSerializer());
            var serializer = new FudgeSerializer(context, typeMap);

            var bob = new Explicit.Sibling { Name = "Bob" };
            var shirley = new Explicit.Sibling { Name = "Shirley" };
            bob.Siblings.Add(shirley);
            shirley.Siblings.Add(bob);                          // Create our cycle

            var msg = serializer.SerializeToMsg(bob);

            var bob2 = (Explicit.Sibling)serializer.Deserialize(msg);
            Assert.NotSame(bob, bob2);
            Assert.Equal(1, bob2.Siblings.Count);
            var shirley2 = (Explicit.Sibling)bob2.Siblings[0];
            Assert.NotSame(shirley, shirley2);
            Assert.Equal(1, shirley2.Siblings.Count);
            Assert.Same(bob2, shirley2.Siblings[0]);
        }

        [Fact]
        public void BaseTypesOutputAsWell_FRN43()
        {
            var serializer = new FudgeSerializer(context);

            var bob = new Explicit.Sibling { Name = "Bob" };

            var msg = serializer.SerializeToMsg(bob);

            var typeNames = msg.GetAllValues<string>(FudgeSerializer.TypeIdFieldOrdinal);
            Assert.Equal(2, typeNames.Count);
            Assert.Equal("Fudge.Tests.Unit.Serialization.Explicit+Sibling", typeNames[0]);
            Assert.Equal("Fudge.Tests.Unit.Serialization.Explicit+Person", typeNames[1]);
        }

        [Fact]
        public void UsesFirstKnownType_FRN43()
        {
            var serializer = new FudgeSerializer(context);

            var msg = context.NewMessage(new Field(0, "Bibble"),
                                         new Field(0, "Fudge.Tests.Unit.Serialization.Explicit+Sibling"),
                                         new Field("name", "Bob"));
            var bob = (Explicit.Sibling)serializer.Deserialize(msg);
            Assert.Equal("Bob", bob.Name);
        }

        [Fact]
        public void InlineAttribute_FRN48()
        {
            var serializer = new FudgeSerializer(context);

            var parent = new InlineParent();
            parent.SetUp();

            var msg = serializer.SerializeToMsg(parent);

            Assert.Equal(FudgeMsgFieldType.Instance, msg.GetByName("In1").Type);
            Assert.Equal(FudgeMsgFieldType.Instance, msg.GetByName("In2").Type);
            Assert.Equal(PrimitiveFieldTypes.SByteType, msg.GetByName("In1ForcedOut").Type);     // References In1 and collapses to byte
            Assert.Equal(FudgeMsgFieldType.Instance, msg.GetByName("Out1").Type);
            Assert.Equal(PrimitiveFieldTypes.SByteType, msg.GetByName("Out2").Type);            // References Out1
            Assert.Equal(FudgeMsgFieldType.Instance, msg.GetByName("Out2ForcedIn").Type);
        }

        [Fact]
        public void SerializingNulls_FRN51()
        {
            var serializer = new FudgeSerializer(context);

            var parent = new InlineParent();

            parent.In1 = null;
            parent.In2 = new Inlined();
            parent.In1ForcedOut = null;
            parent.Out1 = null;
            parent.Out2 = new NotInlined();
            parent.Out2ForcedIn = parent.Out2;

            var msg = serializer.SerializeToMsg(parent);
            var parent2 = (InlineParent)serializer.Deserialize(msg);

            Assert.Null(parent2.In1);
            Assert.NotNull(parent2.In2);
            Assert.Null(parent2.In1ForcedOut);
            Assert.Null(parent2.Out1);
            Assert.NotNull(parent2.Out2);
            Assert.NotNull(parent2.Out2ForcedIn);
        }

        [Fact]
        public void MessagesInObjectsOK()
        {
            // Case here is where a reference may be thrown out by other fields with messages in that aren't deserialized
            var obj1 = new ClassWithMessageIn();
            var obj2 = new ClassWithMessageIn();
            obj2.Message = new FudgeMsg(new Field("a",
                                            new Field("b"),
                                            new Field("c")));   // Add in an arbitrary message
            obj1.Other = obj2;
            obj2.Other = obj1;                                  // We create a cycle so obj2 will refer back to obj1 past the other embedded messages

            var serializer = new FudgeSerializer(context);
            var msg = serializer.SerializeToMsg(obj1);

            var result = (ClassWithMessageIn)serializer.Deserialize(msg);
            Assert.NotSame(result, result.Other);
            Assert.Same(result, result.Other.Other);
        }

        [Fact]
        public void ObjectIdentityNotEquals_FRN65() 
        {
            // Using GetHashCode and Equals is not good enough for testing object identity
            // FRN65Class always returns true for Equals and a constant for GetHashCode
            var obj1 = new FRN65Class { Val = "A", Other = new FRN65Class { Val = "B" } };

            var serializer = new FudgeSerializer(context);
            var msg = serializer.SerializeToMsg(obj1);

            var obj2 = (FRN65Class)serializer.Deserialize(msg);

            Assert.NotSame(obj2, obj2.Other);
        }

        /// <summary>
        /// This makes sure that the serializer and deserializer agree on the order of type relative references
        /// </summary>
        [Fact]
        public void RecursiveDataType_TypeReferences()
        {
            //
            var context = new FudgeContext();
            var serializer = new FudgeSerializer(context);


            var obj = new RecursiveDataType
            {
                Text = "SomeString",
                TemperatureRange = new TemperatureRange { Low = 18, High = 24, Average = 21 },
                Inner = new RecursiveDataType
                            {
                                Text = "SomeOtherString",
                                TemperatureRange = new TemperatureRange { Low = 5, High = 15, Average = 10 },
                                Inner = new RecursiveDataType
                                    {
                                        Text = "AnotherString",
                                        TemperatureRange = new TemperatureRange { Low = 35, High = 40, Average = 37.5 },
                                        Inner = new RecursiveDataType()
                                    }
                            }
            };

            // Serialize it to a MemoryStream
            var stream = new MemoryStream();
            var streamWriter = new FudgeEncodedStreamWriter(context, stream);
            serializer.Serialize(streamWriter, obj);

            // Reset the stream and deserialize a new object from it
            stream.Position = 0;
            var streamReader = new FudgeEncodedStreamReader(context, stream);
            var cycledObj = (RecursiveDataType)serializer.Deserialize(streamReader);

            // Just check a value matches
            Assert.Equal(obj, cycledObj);
        }

        public class TemperatureRange
        {
            public double High { get; set; }
            public double Low { get; set; }
            public double Average { get; set; }

            protected bool Equals(TemperatureRange other)
            {
                return High.Equals(other.High) && Low.Equals(other.Low) && Average.Equals(other.Average);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TemperatureRange) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = High.GetHashCode();
                    hashCode = (hashCode*397) ^ Low.GetHashCode();
                    hashCode = (hashCode*397) ^ Average.GetHashCode();
                    return hashCode;
                }
            }
        }

        #region Inlining test classes

        [FudgeInline]
        private class Inlined
        {
            public bool Value { get; set; }
        }

        private class NotInlined
        {
            public bool Value { get; set; }
        }

        private class InlineParent
        {
            public InlineParent()
            {
            }

            public void SetUp()
            {
                In1 = new Inlined();
                In2 = In1;
                In1ForcedOut = In1;
                Out1 = new NotInlined();
                Out2 = Out1;
                Out2ForcedIn = Out1;
            }

            public Inlined In1 { get; set; }
            public Inlined In2 { get; set; }

            [FudgeInline(false)]    // Override the type
            public Inlined In1ForcedOut { get; set; }

            public NotInlined Out1 { get; set; }
            public NotInlined Out2 { get; set; }

            [FudgeInline]    // Override the type
            public NotInlined Out2ForcedIn { get; set; }
        }

        private class ClassWithMessageIn : IFudgeSerializable
        {
            public ClassWithMessageIn()
            {
                Message = new FudgeMsg();
            }

            public IFudgeFieldContainer Message { get; set; }

            public ClassWithMessageIn Other { get; set; }

            #region IFudgeSerializable Members

            public void Serialize(IAppendingFudgeFieldContainer msg, IFudgeSerializer serializer)
            {
                msg.AddIfNotNull("message", Message);
                msg.AddIfNotNull("other", Other);
            }

            public void Deserialize(IFudgeFieldContainer msg, IFudgeDeserializer deserializer)
            {
                // Skip message
                Other = deserializer.FromField<ClassWithMessageIn>(msg.GetByName("other"));
            }

            #endregion
        }

        private class FRN65Class
        {
            public FRN65Class()
            {
            }

            public string Val { get; set; }

            public FRN65Class Other { get; set; }

            public override int GetHashCode()
            {
                return 16;
            }

            public override bool Equals(object obj)
            {
                return true;
            }
        }

        #endregion

        public class RecursiveDataType
        {
            public RecursiveDataType Inner { get; set; }
            public TemperatureRange TemperatureRange { get; set; }
            public string Text { get; set; }

            protected bool Equals(RecursiveDataType other)
            {
                return Equals(Inner, other.Inner) && Equals(TemperatureRange, other.TemperatureRange) && string.Equals(Text, other.Text);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((RecursiveDataType) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (Inner != null ? Inner.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ (TemperatureRange != null ? TemperatureRange.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ (Text != null ? Text.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }
    }
}
