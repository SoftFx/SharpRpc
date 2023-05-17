// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class HandshakeEncoder
    {
        //private readonly byte[] _bytes;

        public HandshakeEncoder(int maxSize)
        {
            Buffer = new byte[maxSize];
        }

        public int Position { get; private set; }
        public int Length { get; private set; }
        public byte[] Buffer { get; private set; }

        public void Reset()
        {
            Position = 0;
            Length = 0;
        }

        public void Reset(int newLength)
        {
            Position = 0;
            Length = newLength;
        }

        public void SetPosition(int newVal)
        {
            Position = newVal;
        }

        public ArraySegment<byte> GetDataSegment() => new ArraySegment<byte>(Buffer, 0, Length);

        #region Write

        public void Write(byte[] buffer)
        {
            Array.Copy(buffer, 0, Buffer, Position, buffer.Length);
            IncreaseWritePosition(buffer.Length);
        }

        public void Write(byte value)
        {
            Buffer[Position] = value;
            IncreaseWritePosition(1);
        }

        public void Write(ushort value)
        {
#if NET5_0_OR_GREATER
            BitConverter.TryWriteBytes(GetAppendSpan(2), value);
#else
            BitConverter.GetBytes(value).CopyTo(Buffer, Position);
#endif
            IncreaseWritePosition(2);
        }

        public void Write(ShortVersion version)
        {
            Write(version.Major);
            Write(version.Minor);
        }

        public void Write(string value)
        {
            var size = Encoding.UTF8.GetByteCount(value);
            checked { Write((ushort)size); }

#if NET5_0_OR_GREATER
            var byteSize = Encoding.UTF8.GetBytes(value, GetAppendSpan());
            IncreaseWritePosition(byteSize);
#else
            var strBytes = Encoding.UTF8.GetBytes(value);
            Write(strBytes);
            IncreaseWritePosition(strBytes.Length);
#endif
        }

        #endregion

        #region Read

#if NET5_0_OR_GREATER
        public bool TryReadByteArray(int size, out Span<byte> array)
        {
            if (TryGetReadSpan(size, out array))
            {
                Position += size;
                return true;
            }

            return false;
        }
#else
        public bool TryReadByteArray(int size, out ArraySegment<byte> array)
        {
            if (HasEnoughBytes(size))
            {
                array = new ArraySegment<byte>(Buffer, Position, size);
                Position += size;
                return true;
            }

            array = default;
            return false;
        }

#endif

        public bool TryReadString(out string value)
        {
            value = default;

            if (!TryReadUInt16(out var byteSize))
                return false;

#if NET5_0_OR_GREATER
            if (!TryGetReadSpan(byteSize, out var span))
                return false;
                
            value = Encoding.UTF8.GetString(span);
#else
            if (!HasEnoughBytes(byteSize))
                return false;

            value = Encoding.UTF8.GetString(Buffer, Position, byteSize);
#endif

            Position += byteSize;
            return true;
        }

        public bool TryReadUInt16(out ushort value)
        {
            value = default;

#if NET5_0_OR_GREATER
            if (!TryGetReadSpan(2, out var span))
                return false;

            value = BitConverter.ToUInt16(span);
#else
            if (!HasEnoughBytes(2))
                return false;

            value = BitConverter.ToUInt16(Buffer, Position);
#endif

            Position += 2;
            return true;
        }

        public bool TryReadByte(out byte value)
        {
            if (HasEnoughBytes(1))
            {
                value = Buffer[Position++];
                return true;
            }

            value = default;
            return false;
        }

        public bool TryReadVersion(out ShortVersion value)
        {
            if (HasEnoughBytes(2))
            {
                value = new ShortVersion(Buffer[Position], Buffer[Position + 1]);
                Position += 2;
                return true;
            }

            value = default;
            return false;
        }

        private bool HasEnoughBytes(int count)
        {
            return Length - Position >= count;
        }

        #endregion

#if NET5_0_OR_GREATER
        private Span<byte> GetAppendSpan(int size)
        {
            return Buffer.AsSpan().Slice(Position, size);
        }

        private Span<byte> GetAppendSpan()
        {
            return GetAppendSpan(Buffer.Length - Position);
        }

        private bool TryGetReadSpan(int size, out Span<byte> span)
        {
            if (!HasEnoughBytes(size))
            {
                span = default;
                return false;
            }

            span = Buffer.AsSpan().Slice(Position, size);
            return true;
        }
#endif

        private void IncreaseWritePosition(int count)
        {
            Position += count;
            if (Position > Length)
                Length = Position;
        }
    }
}
