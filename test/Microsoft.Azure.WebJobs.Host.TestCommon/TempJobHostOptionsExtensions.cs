// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Temporary class to provide methods removed during the DI work
    /// </summary>
    public static class TempJobHostOptionsExtensions
    {
        public static T GetService<T>(this JobHostOptions options) => throw new NotSupportedException("Using removed/unsupported API");

        public static object GetService(this JobHostOptions options, Type type) => throw new NotSupportedException("Using removed/unsupported API");

        public static void AddService<T>(this JobHostOptions options, T service) => throw new NotSupportedException("Using removed/unsupported API");

        public static void AddService(this JobHostOptions options, Type type, object service) => throw new NotSupportedException("Using removed/unsupported API");

        public static void AddExtension(this JobHostOptions options, object service) => throw new NotSupportedException("Using removed/unsupported API");

        public static IServiceProvider CreateStaticServices(this JobHostOptions options) => throw new NotSupportedException("Using removed/unsupported API");

        public static void UseServiceBus(this JobHostOptions options) => throw new NotSupportedException("Using removed/unsupported API");

        public static void UseServiceBus(this JobHostOptions options, object o) => throw new NotSupportedException("Using removed/unsupported API");
    }
}
