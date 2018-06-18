// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.WebJobs.Hosting
{
    public class JobHostBuilder
    {
        public static IHostBuilder CreateDefault(Action<JobHostOptions> configure)
        {
            configure = configure ?? new Action<JobHostOptions>(o => { });
            return new HostBuilder()
                .ConfigureWebJobsHost(configure);
        }
    }
}
