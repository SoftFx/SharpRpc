// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;

#if STRONG_NAME_BUILD
[assembly: InternalsVisibleTo("SharpRpc.MsTest, PublicKey=f93e83ea42ea164d")]
#else
[assembly: InternalsVisibleTo("SharpRpc.MsTest")]
#endif
