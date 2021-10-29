// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;

#if STRONG_NAME_BUILD
[assembly: InternalsVisibleTo("SharpRpc.MsTest, PublicKey="
            + "002400000480000094000000060200000024000052534131000400000100010005a0317028e4e4"
            + "ec3b228e27f7003fdaf55d72ee1a7e45aadbac5bb2f547ec7ab147ee858dd2b60545eb42ecb2c9"
            + "7fda83c5823d7d00d8251799108ccb21a8c73a818d5c5e0f05bdd1a8cdfc3ba9a33fc7b5c4c22a"
            + "a2ce2572200c71744ccb89293b67c72fb5570e50ea5d7f947739dda769d61ecfbc1e396bf9fa4b"
            + "51c30fde")]
#else
[assembly: InternalsVisibleTo("SharpRpc.MsTest")]
#endif
