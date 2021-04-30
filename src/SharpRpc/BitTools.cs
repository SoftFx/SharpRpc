// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpRpc
{
    internal abstract class BitTools
    {
        public static BitTools Create()
        {
            if (BitConverter.IsLittleEndian)
                return new LeTools();
            else
                return new BeTools();
        }

        public abstract void Write(ushort value, Span<byte> buffer, ref int index);
        public abstract void Write(int value, Span<byte> buffer, ref int index);

        public abstract ushort ReadUshort(Span<byte> buffer, ref int offset);
        public abstract int ReadInt(Span<byte> buffer, ref int offset);

        private class BeTools : BitTools
        {
            public override void Write(ushort value, Span<byte> buffer, ref int index)
            {
                var proxy = new UhsortProxy { Value = value };
                buffer[index++] = proxy.Byte1;
                buffer[index++] = proxy.Byte2;
            }

            public override ushort ReadUshort(Span<byte> buffer, ref int offset)
            {
                var proxy = new UhsortProxy();
                proxy.Byte1 = buffer[offset++];
                proxy.Byte2 = buffer[offset++];
                return proxy.Value;
            }

            public override void Write(int value, Span<byte> buffer, ref int index)
            {
                var proxy = new IntProxy { Value = value };
                buffer[index++] = proxy.Byte1;
                buffer[index++] = proxy.Byte2;
                buffer[index++] = proxy.Byte3;
                buffer[index++] = proxy.Byte4;
            }

            public override int ReadInt(Span<byte> buffer, ref int offset)
            {
                var proxy = new IntProxy();
                proxy.Byte1 = buffer[offset++];
                proxy.Byte2 = buffer[offset++];
                proxy.Byte3 = buffer[offset++];
                proxy.Byte4 = buffer[offset++];
                return proxy.Value;
            }
        }

        private class LeTools : BitTools
        {
            public override void Write(ushort value, Span<byte> buffer, ref int index)
            {
                var proxy = new UhsortProxy { Value = value };
                buffer[index++] = proxy.Byte2;
                buffer[index++] = proxy.Byte1;
            }

            public override ushort ReadUshort(Span<byte> buffer, ref int offset)
            {
                var proxy = new UhsortProxy();
                proxy.Byte2 = buffer[offset++];
                proxy.Byte1 = buffer[offset++];
                return proxy.Value;
            }

            public override void Write(int value, Span<byte> buffer, ref int index)
            {
                var proxy = new IntProxy { Value = value };
                buffer[index++] = proxy.Byte4;
                buffer[index++] = proxy.Byte3;
                buffer[index++] = proxy.Byte2;
                buffer[index++] = proxy.Byte1;
            }

            public override int ReadInt(Span<byte> buffer, ref int offset)
            {
                var proxy = new IntProxy();
                proxy.Byte4 = buffer[offset++];
                proxy.Byte3 = buffer[offset++];
                proxy.Byte2 = buffer[offset++];
                proxy.Byte1 = buffer[offset++];
                return proxy.Value;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct UhsortProxy
        {
            [FieldOffset(0)]
            public ushort Value;
            [FieldOffset(0)]
            public byte Byte1;
            [FieldOffset(1)]
            public byte Byte2;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct IntProxy
        {
            [FieldOffset(0)]
            public int Value;
            [FieldOffset(0)]
            public byte Byte1;
            [FieldOffset(1)]
            public byte Byte2;
            [FieldOffset(2)]
            public byte Byte3;
            [FieldOffset(3)]
            public byte Byte4;
        }
    }
}
