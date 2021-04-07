// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Context for binding to a particular parameter value.
    /// </summary>
    public class ValueBindingContext
    {
        private readonly FunctionBindingContext _functionContext;
        private readonly CancellationToken _cancellationToken;

        // TODO make this private readonly
        public SharedMemoryMetadata _sharedMemoryMetadata;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="functionContext">The context for the parent function.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
        public ValueBindingContext(FunctionBindingContext functionContext, CancellationToken cancellationToken)
        {
            _functionContext = functionContext;
            _cancellationToken = cancellationToken;
            _sharedMemoryMetadata = null;
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="functionContext">The context for the parent function.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
        /// <param name="memoryMapName">The name of the shared memory map where the value is present.</param>
        public ValueBindingContext(FunctionBindingContext functionContext, SharedMemoryMetadata memoryMapName, CancellationToken cancellationToken)
        {
            _functionContext = functionContext;
            _cancellationToken = cancellationToken;
            _sharedMemoryMetadata = memoryMapName;
        }

        /// <summary>
        /// The function context.
        /// </summary>
        public FunctionBindingContext FunctionContext
        {
            get { return _functionContext; }
        }

        /// <summary>
        /// The instance ID of the function being bound to.
        /// </summary>
        public Guid FunctionInstanceId
        {
            get { return _functionContext.FunctionInstanceId; }
        }

        /// <summary>
        /// Gets the function <see cref="CancellationToken"/>.
        /// </summary>
        public CancellationToken FunctionCancellationToken
        {
            get { return _functionContext.FunctionCancellationToken; }
        }

        /// <summary>
        /// Gets the <see cref="CancellationToken"/> to use.
        /// </summary>
        public CancellationToken CancellationToken
        {
            get { return _cancellationToken; }
        }

        /// <summary>
        /// Gets the informatino about where in the shared memory map the value is exists.
        /// This can be used by the storage extension to create an entry for this object in the <see cref="IFunctionDataCache"/>.
        /// </summary>
        public SharedMemoryMetadata SharedMemoryMetadata
        {
            get { return _sharedMemoryMetadata; }
            set { _sharedMemoryMetadata = value; } // TODO remove
        }
    }
}
