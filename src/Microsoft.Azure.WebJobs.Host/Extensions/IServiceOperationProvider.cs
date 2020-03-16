// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Extensions
{
    /// <summary>
    /// Interface for providing Service Operations.
    /// </summary>
    public interface IServiceOperationProvider
    {
        /// <summary>
        /// Gets the operations provided by this implementation
        /// </summary>
        /// <returns>The enumeration of service operations implemented.</returns>
        IEnumerable<ServiceOperation> GetOperations();

        /// <summary>
        /// Invokes asynchronously a service operation with the given input parameters.
        /// </summary>
        /// <param name="operationId">The identifier of the service operation to invoke.</param>
        /// <param name="inputParameters">The input parameters of the operation as JSon.</param>
        /// <returns>The operation response as JSon.</returns>
        Task<JToken> InvokeAsync(string operationId, JToken inputParameters);
    }
}
