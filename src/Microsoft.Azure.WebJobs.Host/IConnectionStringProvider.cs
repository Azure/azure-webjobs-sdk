// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Defines an interface for providing connection strings.
    /// </summary>
    // TODO: We should be able to remove this and rely strictly on IConfiguration.
    public interface IConnectionStringProvider
    {
        /// <summary>
        /// Get the connection string for the specified name.
        /// </summary>
        /// <param name="connectionStringName">The connection string name.</param>
        /// <returns>The connection string if found.</returns>
        string GetConnectionString(string connectionStringName);
    }
}
