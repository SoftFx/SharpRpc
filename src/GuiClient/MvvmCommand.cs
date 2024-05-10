// Copyright © 2022 Soft-Fx. All rights reserved.
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
using System.Windows.Input;

namespace GuiClient
{
    internal class MvvmCommand : ICommand
    {
        private bool _canExecute = true;
        private readonly Action<object?> _commandImpl;

        public MvvmCommand(Action<object?> commandImpl)
        {
            _commandImpl = commandImpl;
        }

        public bool Enabled
        {
            get => _canExecute;
            set
            {
                if (value != _canExecute)
                {
                    _canExecute = value;
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute;
        }

        public void Execute(object? parameter)
        {
            _commandImpl(parameter);   
        }
    }

    internal class AsyncMvvmCommand : ICommand
    {
        private bool _isEnabled = true;
        private readonly Func<object?, Task> _commandImpl;
        private bool _isRunning;

        public AsyncMvvmCommand(Func<object?, Task> commandImpl)
        {
            _commandImpl = commandImpl;
        }

        public bool Enabled
        {
            get => _isEnabled;
            set
            {
                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _isEnabled && !_isRunning;
        }

        public async void Execute(object? parameter)
        {
            _isRunning = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);

            await _commandImpl(parameter);

            _isRunning = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
