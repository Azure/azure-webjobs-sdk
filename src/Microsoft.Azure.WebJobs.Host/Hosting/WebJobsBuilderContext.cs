// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs
{
    public class WebJobsBuilderContext
    {
        public IConfiguration Configuration { get; set; }

        public string EnvironmentName { get; set; }

        public string ApplicationRootPath { get; set; }
    }
}
