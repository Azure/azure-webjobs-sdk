// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Config
{
    public interface IWebJobsExtensionOptionsConfiguration<TOptions> : IConfigureOptions<TOptions> where TOptions : class
    {
        IExtensionInfo ExtensionInfo { get; }
        Type OptionType { get; }
    }
}
