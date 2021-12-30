// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    public interface IWebJobsExtensionOptionDataSource
    {
        object Register(string section, string subSection, object config);

        void Clear();

        JObject GetOptions(string section);
    }
}
