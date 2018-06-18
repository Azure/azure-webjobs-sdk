// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Hosting
{
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
    public class WebJobsStartupAttribute : Attribute
    {
        public WebJobsStartupAttribute(Type startupType)
        {
            if (startupType == null)
            {
                throw new ArgumentNullException(nameof(startupType));
            }

            if (!typeof(IWebJobsStartup).IsAssignableFrom(startupType))
            {
                throw new ArgumentException($@"""{startupType}"" does not implement {typeof(IWebJobsStartup)}.", nameof(startupType));
            }

            WebJobsStartupType = startupType;
        }

        public Type WebJobsStartupType { get; }
    }
}
