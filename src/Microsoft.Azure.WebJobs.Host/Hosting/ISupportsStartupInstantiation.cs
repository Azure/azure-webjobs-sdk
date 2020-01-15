﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Hosting;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    interface ISupportsStartupInstantiation
    {
        IWebJobsStartup CreateStartupInstance(Type startupType);
    }
}
