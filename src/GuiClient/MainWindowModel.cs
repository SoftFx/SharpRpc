// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TestCommon;

namespace GuiClient
{
    internal class MainWindowModel : INotifyPropertyChanged
    {
        private bool _ssl;
        private string _address = "localhost";
        private bool _isConnected;
        private bool _isConnecting;
        private FunctionTestContract_Gen.Client? _client;

        public MainWindowModel()
        {
            Connect = new MvvmCommand(ConnectRoutine);
            Disconnect = new MvvmCommand(DisconnectRoutine);
            SyncCall = new MvvmCommand(DoSyncCall);
            AsyncCall = new AsyncMvvmCommand(DoAsyncCall);

            UpdateConnectStatus();
        }

        public string Address
        {
            get => _address;
            set
            {
                if (_address != value)
                {
                    _address = value;
                    NotifyPropertyChanged(nameof(Address));
                    UpdateConnectStatus();
                }
            }
        }

        public bool CanChangeAddress => !_isConnected && !_isConnecting;
        public bool CanRunCommands => _isConnected;

        public MvvmCommand Connect { get; }
        public MvvmCommand Disconnect { get; }

        public MvvmCommand SyncCall { get; }
        public AsyncMvvmCommand AsyncCall { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private async void ConnectRoutine(object? _)
        {
            var security = _ssl ? new SslSecurity(NullCertValidator) : TcpSecurity.None;
            var port = 812;
            var serviceName = _ssl ? "func/ssl" : "func";
            var endpoint = new TcpClientEndpoint(new DnsEndPoint(_address, port), serviceName, security);

            if (_ssl)
                endpoint.Credentials = new BasicCredentials("Admin", "zzzz");

            var callback = new CallbackHandler();
            _client = FunctionTestContract_Gen.CreateClient(endpoint, callback);

            _isConnecting = true;
            UpdateConnectStatus();

            var connectResult =  await _client.Channel.TryConnectAsync();

            if (connectResult.IsOk)
                _isConnected = true;
            _isConnecting = false;
            UpdateConnectStatus();
        }

        private async void DisconnectRoutine(object? _)
        {
            _isConnecting = true;
            UpdateConnectStatus();

            if (_client != null)
                await _client.Channel.CloseAsync();

            _isConnecting = false;
            _isConnected = false;
            UpdateConnectStatus();
        }

        private void UpdateConnectStatus()
        {
            Connect.Enabled = !string.IsNullOrWhiteSpace(_address) && !_isConnecting && !_isConnected;
            Disconnect.Enabled = _isConnected && !_isConnecting;

            NotifyPropertyChanged(nameof(CanChangeAddress));
            NotifyPropertyChanged(nameof(CanRunCommands));
        }

        private void DoSyncCall(object? _)
        {
            var callResult = _client?.Try.TestCall1(10, "11");
        }

        private async Task DoAsyncCall(object? _)
        {
            if (_client != null)
            {
                var callResult = await _client.TryAsync.TestCall1(10, "11");
            }
        }

        private static bool NullCertValidator(object? sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private void NotifyPropertyChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public class CallbackHandler : FunctionTestContract_Gen.CallbackServiceBase
        {
#if NET5_0_OR_GREATER
            public override ValueTask TestCallbackNotify1(int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return new ValueTask();
            }

            public override ValueTask TestCallback1(CallContext context, int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return new ValueTask();
            }

            public override ValueTask<int> TestCallback2(CallContext context, int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return ValueTask.FromResult(21);
            }

            public override ValueTask<string> TestCallback3(CallContext context, int p1, string p2)
            {
                throw new Exception("Test Exception");
            }
#else
            public override Task TestCallbackNotify1(int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return Task.CompletedTask;
            }

            public override Task TestCallback1(CallContext context, int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return Task.CompletedTask;
            }

            public override Task<int> TestCallback2(CallContext context, int p1, string p2)
            {
                if (p1 != 10 || p2 != "11")
                    throw new Exception("Invalid input!");

                return Task.FromResult(21);
            }

            public override Task<string> TestCallback3(CallContext context, int p1, string p2)
            {
                throw new Exception("Test Exception");
            }
#endif
        }
    }
}
