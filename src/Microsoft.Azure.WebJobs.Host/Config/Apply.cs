// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Host.Config
{
    // Used to register a converter that applies a New value to an existing value. 
    // This is common for Streams. The new value is written to an existing stream instantation. 
    class Apply<TNew, TExisting>
    {
        public TNew Value;
        public TExisting Existing;
    }
}
