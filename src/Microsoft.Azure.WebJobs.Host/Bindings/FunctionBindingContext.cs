// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.Azure.WebJobs.Host.Protocols;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Provides binding context for all bind operations scoped to a particular
    /// function invocation.
    /// </summary>
    public class FunctionBindingContext
    {
        private readonly Guid _functionInstanceId;
        private readonly CancellationToken _functionCancellationToken;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="functionInstanceId">The instance ID of the function being bound to.</param>
        /// <param name="functionCancellationToken">The <see cref="CancellationToken"/> to use.</param>
        /// <param name="userLogger">The user logger.</param>
        /// <param name="functionDescriptor">Current function being executed. </param>
        public FunctionBindingContext(
            Guid functionInstanceId,
            CancellationToken functionCancellationToken,
            FunctionDescriptor functionDescriptor = null)
        {
            _functionInstanceId = functionInstanceId;
            _functionCancellationToken = functionCancellationToken;

            this.MethodName = functionDescriptor?.LogName;
        }

        /// <summary>
        /// Gets the instance ID of the function being bound to.
        /// </summary>
        public Guid FunctionInstanceId => _functionInstanceId;

        /// <summary>
        /// Gets the <see cref="CancellationToken"/> to use.
        /// </summary>
        public CancellationToken FunctionCancellationToken => _functionCancellationToken;

        /// <summary>
        /// The short name of the current function. 
        /// </summary>
        public string MethodName { get; private set; }
    }
}
