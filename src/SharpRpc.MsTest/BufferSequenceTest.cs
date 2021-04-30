// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpRpc.Lib;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.MsTest
{
    [TestClass]
    public class BufferSequenceTest
    {
        [DataTestMethod]
        [DataRow(128)]
        [DataRow(64)]
        [DataRow(60)]
        [DataRow(30)]
        [DataRow(8)]
        [DataRow(7)]
        [DataRow(5)]
        public void BuffeSequenceTest_Intergrity(int partitionSize)
        {
            var initialBuffer = ArrayExt.ByteSequence(128);
            var partitions = initialBuffer.Partition(partitionSize);
            var segments = partitions.Select(p => new ArraySegment<byte>(p)).ToArray();

            var sequenceAdapter = new BufferSequence<byte>();
            sequenceAdapter.AddRange(segments);

            var sequence = sequenceAdapter.GetSequence();
            var resultingBuffer = ReadWholeSequnce(sequence);

            CollectionAssert.AreEqual(initialBuffer, resultingBuffer);
        }

        private byte[] ReadWholeSequnce(ReadOnlySequence<byte> sequence)
        {
            var sReader = new System.Buffers.SequenceReader<byte>(sequence);
            var result = new byte[sReader.Length];

            if (!sReader.TryCopyTo(result))
                throw new Exception("Failed to read sequence!");

            return result;
        }
    }
}
