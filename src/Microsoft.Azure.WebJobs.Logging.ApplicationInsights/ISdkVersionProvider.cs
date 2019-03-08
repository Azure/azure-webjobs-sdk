// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Reflection;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    public interface ISdkVersionProvider
    {
        string GetSdkVersion();
    }

    internal class WebJobsSdkVersionProvider : ISdkVersionProvider
    {
        private readonly string sdkVersion = "webjobs: " + GetAssemblyFileVersion(typeof(JobHost).Assembly);

        public string GetSdkVersion()
        {
            return sdkVersion;
        }

        internal static string GetAssemblyFileVersion(Assembly assembly)
        {
            AssemblyFileVersionAttribute fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
            return fileVersionAttr?.Version ?? LoggingConstants.Unknown;
        }
    }
}
