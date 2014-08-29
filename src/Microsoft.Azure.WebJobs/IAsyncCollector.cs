// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Abstraction over a table.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    public interface IAsyncCollector<in T>
    {
        /// <summary>
        /// Adds an item to the table.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        Task AddAsync(T item);
    }
}
