// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Text;

namespace SharpRpc.Serialization
{
    internal class SimplifiedDecoder
    {
        private readonly ISegmetedBufferEnumerator _enumator;

        public SimplifiedDecoder(ISegmetedBufferEnumerator data)
        {
            _enumator = data;
        }

        public bool TryReadString(out string value, out long byteLength)
        {
            value = null;
            byteLength = 0;

            if (!TryReadUInt16(out var length))
                return false;

            byteLength = length + 2;

            var bytesLeftInSegment = _enumator.PageSize - _enumator.PageIndex;

            if (bytesLeftInSegment >= length)
            {
                value = Encoding.UTF8.GetString(_enumator.Page, _enumator.PageOffset + _enumator.PageIndex, length);
                _enumator.Advance(length);
                //MoveToNextPageIfRequired();
                return true;
            }
            else
            {
                if (!TryReadByteArray(length, out var buffer))
                    return false;

                value = Encoding.UTF8.GetString(buffer);
                return true;
            }
        }

        public bool TryReadByteArray(long count, out byte[] bytes)
        {
            bytes = new byte[count];
            return TryReadByteArray(new ArraySegment<byte>(bytes));
        }

        public bool TryReadByteArray(ArraySegment<byte> targetBuffer)
        {
            var toRead = targetBuffer.Count;
            var index = 0;

            while (toRead > 0)
            {
                var bytesLeftInSegment = _enumator.PageSize - _enumator.PageIndex;
                var copySize = Math.Min(bytesLeftInSegment, toRead);
                Array.Copy(_enumator.Page, _enumator.PageOffset + _enumator.PageIndex, targetBuffer.Array, targetBuffer.Offset + index, copySize);
                index += copySize;
                toRead -= copySize;
                _enumator.Advance(copySize);
            }

            return true;
        }

        public bool TryReadUInt16(out ushort value)
        {
            value = 0;

            if (!TryReadByte(out var byte1))
                return false;

            if (!TryReadByte(out var byte2))
                return false;

            value = BitTools.Instance.GetUshort(byte1, byte2);
            return true;
        }

        public bool TryReadByte(out byte value)
        {
            if (_enumator.Page == null)
            {
                value = default;
                return false;
            }

            value = _enumator.Page[_enumator.PageOffset + _enumator.PageIndex];
            _enumator.Advance(1);
            //MoveToNextPageIfRequired();
            return true;
        }

        //private void MoveToNextPageIfRequired()
        //{
        //    if (_pageIndex >= _currentPage.Count)
        //        MoveToNextPage();
        //}

        //private void MoveToNextPage()
        //{
        //    _pageNo++;

        //    if (_pageIndex < _data.PageSize)
        //        _currentPage = _data[_pageNo];
        //    else
        //        _currentPage = default;

        //}
    }
}
