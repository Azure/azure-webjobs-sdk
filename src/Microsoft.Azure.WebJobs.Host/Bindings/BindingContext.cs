﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Queues;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    public class BindingContext
    {
        private readonly FunctionBindingContext _functionContext;
        private readonly IReadOnlyDictionary<string, object> _bindingData;
        private readonly CancellationToken _cancellationToken;

        private AmbientBindingContext _ambientContext;
        private ValueBindingContext _valueContext;

        public BindingContext(ValueBindingContext valueContext, IReadOnlyDictionary<string, object> bindingData)
        {
            _functionContext = valueContext.FunctionContext;
            _bindingData = bindingData;
            _cancellationToken = valueContext.CancellationToken;

            _valueContext = valueContext;
        }

        public BindingContext(AmbientBindingContext ambientContext, CancellationToken cancellationToken)
        {
            _functionContext = ambientContext.FunctionContext;
            _bindingData = ambientContext.BindingData;
            _cancellationToken = cancellationToken;

            _ambientContext = ambientContext;
        }

        public Guid FunctionInstanceId
        {
            get { return _functionContext.FunctionInstanceId; }
        }

        public CancellationToken FunctionCancellationToken
        {
            get { return _functionContext.FunctionCancellationToken; }
        }

        public TextWriter ConsoleOutput
        {
            get { return _functionContext.ConsoleOutput; }
        }

        public IReadOnlyDictionary<string, object> BindingData
        {
            get { return _bindingData; }
        }

        public CancellationToken CancellationToken
        {
            get { return _cancellationToken; }
        }

        public AmbientBindingContext AmbientContext
        {
            get
            {
                if (_ambientContext == null)
                {
                    _ambientContext = new AmbientBindingContext(_functionContext, _bindingData);
                }

                return _ambientContext;
            }
        }

        public ValueBindingContext ValueContext
        {
            get
            {
                if (_valueContext == null)
                {
                    _valueContext = new ValueBindingContext(_functionContext, _cancellationToken);
                }

                return _valueContext;
            }
        }
    }
}
