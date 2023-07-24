// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class StreamOptions
    {
        public StreamOptions() { }

        public const ushort DefaultWindowsSize = 100;

        internal StreamOptions(IOpenStreamRequest request)
        {
            WindowSize = request.WindowSize ?? DefaultWindowsSize;
        }

        /// <summary>
        /// Specifies how many items can be in transmission simultaneously.
        /// </summary>
        public ushort WindowSize { get; set; } = DefaultWindowsSize;

        public override string ToString()
        {
            return $"(WindowSize = {WindowSize})";
        }
    }

    public class DuplexStreamOptions
    {
        /// <summary>
        /// Specifies how many items can be in transmission simultaneously for the input stream.
        /// </summary>
        public ushort InputWindowSize { get; set; }

        /// <summary>
        /// Specifies how many items can be in transmission simultaneously for the output stream.
        /// </summary>
        public ushort OutputWindowSize { get; set; }

        internal StreamOptions GetInputOptions()
        {
            return new StreamOptions() { WindowSize = InputWindowSize };
        }

        internal StreamOptions GetOutputOptions()
        {
            return new StreamOptions() { WindowSize = InputWindowSize };
        }
    }
}
