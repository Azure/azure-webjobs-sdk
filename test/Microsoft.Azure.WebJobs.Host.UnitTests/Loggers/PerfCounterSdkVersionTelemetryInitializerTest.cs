// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class PerfCounterSdkVersionTelemetryInitializerTest
    {
        [Fact]
        public void Initializer_OnlyModifiedPerfCounterTelemetry()
        {
            // Create a RequestTelemetry with SDKVersion.
            var request = new RequestTelemetry
            {
                ResponseCode = "200",
                Name = "POST /api/somemethod",
            };

            string originalSdkVersion = "azurefunctions:2.9.1-26132";
            request.Context.GetInternalContext().SdkVersion = originalSdkVersion;

            // Apply initializer
            var initializer = new PerfCounterSdkVersionTelemetryInitializer();
            initializer.Initialize(request);

            // Validate that original version is un-touched, as this initializer is only expected to modify PerfCounter telemetry
            Assert.Equal(originalSdkVersion, request.Context.GetInternalContext().SdkVersion);
        }

        [Fact]
        public void Initializer_AddsPrefix_PerfCounterTelemetry()
        {
            // Create a PerfCounter telemetry with some SDK version
            string originalVersion = "azwapccore:2.9.1-26132";
            var performanceCounterTelemetry = new PerformanceCounterTelemetry("cat", "counter", "instance", 100.00);
            performanceCounterTelemetry.Context.GetInternalContext().SdkVersion = originalVersion;

            // Apply Initializer
            var initializer = new PerfCounterSdkVersionTelemetryInitializer();
            initializer.Initialize(performanceCounterTelemetry);
            
            var actualSdkVersion = performanceCounterTelemetry.Context.GetInternalContext().SdkVersion;
            // Validate that "f_" is prefixed
            Assert.StartsWith("f_", actualSdkVersion);
            // And validate that original version is kept after "f_"
            Assert.EndsWith(originalVersion, actualSdkVersion);
        }

        [Fact]
        public void Initializer_IsIdempotent()
        {
            // Create a PerfCounter telemetry with SDK version starting with "f_"
            string originalVersion = "f_azwapccore:2.9.1-26132";
            var performanceCounterTelemetry = new PerformanceCounterTelemetry("cat", "counter", "instance", 100.00);            
            performanceCounterTelemetry.Context.GetInternalContext().SdkVersion = originalVersion;

            // Apply Initializer, more than once.
            var initializer = new PerfCounterSdkVersionTelemetryInitializer();
            initializer.Initialize(performanceCounterTelemetry);
            initializer.Initialize(performanceCounterTelemetry);

            // Validate that initializer does not modify the SDKVersion as it is already prefixed with "f_"
            var actualSdkVersion = performanceCounterTelemetry.Context.GetInternalContext().SdkVersion;
            Assert.Equal(originalVersion, actualSdkVersion);
        }

        [Fact]
        public void Initializer_DoesNotThrowOnEmptyOrNullVersion_PerfCounterTelemetry()
        {
            // Create a PerfCounter telemetry.
            var performanceCounterTelemetry = new PerformanceCounterTelemetry("cat", "counter", "instance", 100.00);
            var initializer = new PerfCounterSdkVersionTelemetryInitializer();

            // Assign Empty Version
            performanceCounterTelemetry.Context.GetInternalContext().SdkVersion = string.Empty;

            // Apply Initializer            
            initializer.Initialize(performanceCounterTelemetry);

            // Assign Null Version
            performanceCounterTelemetry.Context.GetInternalContext().SdkVersion = null;

            // Apply Initializer            
            initializer.Initialize(performanceCounterTelemetry);

            // Nothing to validate. If an exception was thrown, test would fail.
        }

        [Fact]
        public void Initializer_DoesNotThrowOnNull_PerfCounterTelemetry()
        {            
            // Apply Initializer
            var initializer = new PerfCounterSdkVersionTelemetryInitializer();
            initializer.Initialize(null);
            // Nothing to validate. If an exception was thrown, test would fail.
        }
    }
}
