// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.MsTest
{
    public static class ArrayExt
    {
        public static T[] Slice<T>(this T[] srcArray, int startIndex, int length)
        {
            var chunk = new T[length];
            Array.Copy(srcArray, startIndex, chunk, 0, length);
            return chunk;
        }

        public static T[] Add<T>(this T[] array1, T[] array2)
        {
            var result = new T[array1.Length + array2.Length];

            Buffer.BlockCopy(array1, 0, result, 0, array1.Length);
            Buffer.BlockCopy(array2, 0, result, array1.Length, array2.Length);

            return result;
        }

        public static List<T[]> Partition<T>(this T[] srcArray, int partitionSize)
        {
            var partitions = new List<T[]>();

            for (int i = 0; i < srcArray.Length;)
            {
                var itemsLeft = srcArray.Length - i;
                var pSize = Math.Min(partitionSize, itemsLeft);
                var partition = new T[pSize];

                Buffer.BlockCopy(srcArray, i, partition, 0, pSize);
                partitions.Add(partition);

                i += pSize;
            }

            return partitions;
        }

        public static T[] Join<T>(IReadOnlyList<ArraySegment<T>> segments)
        {
            var totalSize = 0;

            foreach (var seg in segments)
                totalSize += seg.Count;

            var result = new T[totalSize];
            var offset = 0;

            foreach (var seg in segments)
            {
                Buffer.BlockCopy(seg.Array, seg.Offset, result, offset, seg.Count);
                offset += seg.Count;
            }

            return result;
        }

        public static byte[] ByteSequence(int length)
        {
            var buffer = new byte[length];

            for (int i = 0; i < length; i++)
                buffer[i] = (byte)(i % byte.MaxValue);

            return buffer;
        }
    }
}
