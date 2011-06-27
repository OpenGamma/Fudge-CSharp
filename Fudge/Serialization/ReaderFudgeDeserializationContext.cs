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
using System.IO;
using Fudge.Encodings;
using Fudge.Util;

namespace Fudge.Serialization
{
    /// <summary>
    /// Provides an implementation of <see cref="FudgeDeserializationContext"/> which reads from arbitrary <see cref="IFudgeStreamReader"/>s
    /// </summary>
    /// <remarks>
    /// You should not need to use this class directly. <see cref="FudgeSerializer.Deserialize(Fudge.IFudgeStreamReader)"/>
    /// </remarks>
    /// <notes>
    /// REVIEW 2011-06-27 simon-og -- The message walking could be done whilst deserializing, but for cases where we read from a <see cref="MemoryStream"/> this seems to be slower
    /// </notes>
    internal class ReaderFudgeDeserializationContext : FudgeDeserializationContext
    {
        private readonly FudgeMsgStreamWriter msgWriter;
        private readonly FudgeStreamPipe pipe;

        public ReaderFudgeDeserializationContext(FudgeContext context, SerializationTypeMap typeMap, IFudgeStreamReader reader, IFudgeTypeMappingStrategy typeMappingStrategy)
            :base(context, typeMap, typeMappingStrategy)
        {
            this.msgWriter = new FudgeMsgStreamWriter(context);
            this.pipe = new FudgeStreamPipe(reader, msgWriter);
        }

        protected override FudgeMsg GetMessage()
        {
            // We simply return the first object
            pipe.ProcessOne();
            return msgWriter.DequeueMessage();
        }
    }
}