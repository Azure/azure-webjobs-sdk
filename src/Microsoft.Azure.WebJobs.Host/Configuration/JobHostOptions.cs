// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Executors;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Represents the configuration settings for a <see cref="JobHost"/>.
    /// </summary>
    public sealed class JobHostOptions
    {
        private string _hostId;

        /// <summary>
        /// Returns true if <see cref="UseDevelopmentSettings"/> has been called on this instance.
        /// </summary>
        internal bool UsingDevelopmentSettings { get; set; }

        
        /// <summary>
        /// Gets or sets a value indicating whether the host should be able to start partially
        /// when some functions are in error. The default is false.
        /// </summary>
        /// <remarks>
        /// Normally when a function encounters an indexing error or its listener fails to start
        /// the error will propagate and the host will not start. However, with this option set,
        /// the host will be allowed to start in "partial" mode:
        ///   - Functions without errors will run normally
        ///   - Functions with indexing errors will not be running
        ///   - Functions listener startup failures will be retried in the background
        ///     until they start.
        /// </remarks>
        public bool AllowPartialHostStartup { get; set; }

        /// <summary>Gets or sets the host ID.</summary>
        /// <remarks>
        /// <para>
        /// All host instances that share the same host ID must be homogeneous. For example, they must use the same
        /// storage accounts and have the same list of functions. Host instances with the same host ID will scale out
        /// and share handling of work such as BlobTrigger and run from dashboard processing and providing a heartbeat
        /// to the dashboard indicating that an instance of the host running.
        /// </para>
        /// <para>
        /// If this value is <see langword="null"/> on startup, a host ID will automatically be generated based on the assembly
        /// name of the first function, and that host ID will be made available via this property after the host has fully started.
        /// </para>
        /// <para>
        /// If non-homogeneous host instances share the same first function assembly,
        /// this property must be set explicitly; otherwise, the host instances will incorrectly try to share work as if
        /// they were homogeneous.
        /// </para>
        /// </remarks>
        public string HostId
        {
            get
            {
                return _hostId;
            }
            set
            {
                if (value != null && !HostIdValidator.IsValid(value))
                {
                    throw new ArgumentException(HostIdValidator.ValidationMessage, "value");
                }

                _hostId = value;
            }
        }
    }
}
