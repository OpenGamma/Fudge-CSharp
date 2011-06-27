/* <!--
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

namespace Fudge.Serialization
{
    /// <summary>
    /// Provides an implementation of <see cref="IFudgeDeserializer"/> used by the <see cref="FudgeSerializer"/>.
    /// </summary>
    /// <remarks>
    /// You should not need to use this class directly.
    /// </remarks>
    internal class StreamingFudgeDeserializationContext : FudgeDeserializationContextBase
    {
        private readonly FudgeStreamReaderMsgWriter msgWriter;

        public StreamingFudgeDeserializationContext(FudgeContext context, SerializationTypeMap typeMap, IFudgeStreamReader reader, IFudgeTypeMappingStrategy typeMappingStrategy)
            : this(context, typeMap, typeMappingStrategy, new FudgeStreamReaderMsgWriter(context, reader))
        {
        }

        private StreamingFudgeDeserializationContext(FudgeContext context, SerializationTypeMap typeMap, IFudgeTypeMappingStrategy typeMappingStrategy, FudgeStreamReaderMsgWriter msgWriter)
            : base(context, typeMap, typeMappingStrategy, msgWriter.Map)
        {
            this.msgWriter = msgWriter;
            msgWriter.DeserializationContext = this;

        }

        public override object DeserializeGraph(Type typeHint)
        {
            // We simply return the first object
            msgWriter.ProcessOne();
            
            object result = GetFromRef(0, typeHint);
            
            return result;
        }

        public bool Finished(FudgeMsg fudgeMsg, int index)
        {
            Type objectType = GetObjectType(index, null, fudgeMsg, false);
            if (objectType != null)
            {
                DeserializeFromMessageImpl(index, fudgeMsg, objectType);
                return true;
            }
            return false;
        }
    }
}
