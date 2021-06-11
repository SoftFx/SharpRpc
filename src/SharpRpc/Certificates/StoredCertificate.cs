// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class StoredCertificate : CertificateSource
    {
        private StoreLocation _location;
        private StoreName _name;
        private X509FindType _findType;
        private object _findVal;

        public StoredCertificate(StoreLocation storeLocation, StoreName storeName, X509FindType findType, object findValue)
        {
            _location = storeLocation;
            _name = storeName;
            _findType = findType;
            _findVal = findValue;
        }

        public override X509Certificate2 GetCertificate()
        {
            try
            {
                var store = new X509Store(_name, _location);
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                try
                {
                    var found = store.Certificates.Find(_findType, _findVal, false);

                    if (found.Count == 0)
                        throw new CertLoadError("Cannnot find certificate with secified search criteria in store.");

                    return found[0];
                }
                finally
                {
                    store.Close();
                    store.Dispose();
                }
            }
            catch (ArgumentException aex)
            {
                throw new CertLoadError(aex);
            }
            catch (CryptographicException ex)
            {
                throw new CertLoadError(ex);
            }
        }
    }
}
