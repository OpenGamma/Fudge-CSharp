﻿/**
 * Copyright (C) 2009 - 2009 by OpenGamma Inc.
 *
 * Please see distribution for license.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenGamma.Fudge.Taxon;
using System.IO;

namespace OpenGamma.Fudge.Types
{
    public abstract class FudgeArrayFieldTypeBase<T> : FudgeFieldType<T[]>
    {
        private readonly int elementSize;
        private readonly Action<BinaryWriter, T> writer;
        private readonly Func<BinaryReader, T> reader;

        public FudgeArrayFieldTypeBase(int typeId, int elementSize, Action<BinaryWriter, T> writer, Func<BinaryReader, T> reader)
            : base(typeId, true, 0)
        {
            this.elementSize = elementSize;
            this.writer = writer;
            this.reader = reader;
        }

        public override int GetVariableSize(T[] value, IFudgeTaxonomy taxonomy)
        {
            return value.Length * elementSize;
        }

        public override T[] ReadTypedValue(BinaryReader input, int dataSize) //throws IOException
        {
            int nElements = dataSize / elementSize;
            T[] result = new T[nElements];
            for (int i = 0; i < nElements; i++)
            {
                result[i] = reader(input);
            }
            return result;
        }

        public override void WriteValue(BinaryWriter output, T[] value, IFudgeTaxonomy taxonomy, short taxonomyId)  //throws IOException
        {
            foreach (T element in value)
            {
                writer(output, element);
            }
        }

        public override object Minimize(object value, ref FudgeFieldType type)
        {
            T[] array = value as T[];
            
            if (array != null && array.Length == 0)
            {
                type = IndicatorFieldType.Instance;
                value = IndicatorType.Instance;
            }

            return value;
        }

        public override object ConvertValueFrom(object value)
        {
            if (value == IndicatorType.Instance)
                return new T[0];

            return base.ConvertValueFrom(value);
        }
    }
}