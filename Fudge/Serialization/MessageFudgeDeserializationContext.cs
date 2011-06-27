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

namespace Fudge.Serialization
{
    /// <summary>
    /// Provides an optimized implementation of <see cref="FudgeDeserializationContext"/> which operates on a <see cref="FudgeMsg"/>.
    /// This allows for significantly reduced memory usage.
    /// </summary>
    /// <remarks>
    /// You should not need to use this class directly. <see cref="FudgeSerializer.Deserialize(Fudge.FudgeMsg)"/>
    /// </remarks>
    internal class MessageFudgeDeserializationContext : FudgeDeserializationContext
    {
        private readonly FudgeMsg msg;

        public MessageFudgeDeserializationContext(FudgeContext context, SerializationTypeMap typeMap, IFudgeTypeMappingStrategy typeMappingStrategy, FudgeMsg msg) : base(context, typeMap, typeMappingStrategy)
        {
            this.msg = msg;
        }

        protected override FudgeMsg GetMessage()
        {
            return msg;
        }
    }
}