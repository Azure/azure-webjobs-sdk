// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

#if PUBLICPROTOCOL
namespace Microsoft.Azure.WebJobs.Protocols
#else
namespace Microsoft.Azure.WebJobs.Host.Protocols
#endif
{
    /// <summary>Represents a function's execution failure information.</summary>
#if PUBLICPROTOCOL
    public class FunctionFailure
#else
    internal class FunctionFailure
#endif
    {
        [JsonIgnore]
        // Used for in-memory tests only. For serialized protocol data, the other properties are used instead.
        internal Exception Exception { get; set; }

        /// <summary>Gets or sets the name of the type of exception that occurred.</summary>
        public string ExceptionType { get; set; }

        /// <summary>Gets or sets the details of the exception that occurred.</summary>
        public string ExceptionDetails { get; set; }

        internal static FunctionFailure FromException(Exception ex)
        {
            return new FunctionFailure
            {
                Exception = ex,
                ExceptionType = ex.GetType().FullName,
                ExceptionDetails = ex is ObjectDisposedException ? ex.ToString() : ex.ToDetails() // Do not format ObjectDisposedExceptions to aid in debugging
            };
        }
    }
}
