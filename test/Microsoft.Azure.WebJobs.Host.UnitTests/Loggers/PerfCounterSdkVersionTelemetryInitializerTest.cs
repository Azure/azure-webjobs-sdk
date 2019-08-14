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
    public class MetricSdkVersionTelemetryInitializerTest
    {
        [Fact]
        public void Initializer_OnlyModifiedMetricTelemetry()
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
            var initializer = new MetricSdkVersionTelemetryInitializer();
            initializer.Initialize(request);

            // Validate that original version is un-touched, as this initializer is only expected to modify Metric telemetry
            Assert.Equal(originalSdkVersion, request.Context.GetInternalContext().SdkVersion);
        }

        [Fact]
        public void Initializer_AddsPrefix_MetricTelemetry()
        {
            // Create a Metric telemetry with some SDK version
            string originalVersion = "azwapccore:2.9.1-26132";
            var metricTelemetry = new MetricTelemetry("metric", 100.00);
            metricTelemetry.Context.GetInternalContext().SdkVersion = originalVersion;

            // Apply Initializer
            var initializer = new MetricSdkVersionTelemetryInitializer();
            initializer.Initialize(metricTelemetry);
            
            var actualSdkVersion = metricTelemetry.Context.GetInternalContext().SdkVersion;
            // Validate that "af_" is prefixed
            Assert.StartsWith("af_", actualSdkVersion);
            // And validate that original version is kept after "af_"
            Assert.EndsWith(originalVersion, actualSdkVersion);
        }

        [Fact]
        public void Initializer_IsIdempotent()
        {
            // Create a Metric telemetry with SDK version starting with "af_"
            string originalVersion = "af_azwapccore:2.9.1-26132";
            var metricTelemetry = new MetricTelemetry("metric", 100.00);
            metricTelemetry.Context.GetInternalContext().SdkVersion = originalVersion;

            // Apply Initializer, more than once.
            var initializer = new MetricSdkVersionTelemetryInitializer();
            initializer.Initialize(metricTelemetry);
            initializer.Initialize(metricTelemetry);

            // Validate that initializer does not modify the SDKVersion as it is already prefixed with "af_"
            var actualSdkVersion = metricTelemetry.Context.GetInternalContext().SdkVersion;
            Assert.Equal(originalVersion, actualSdkVersion);
        }

        [Fact]
        public void Initializer_DoesNotThrowOnEmptyOrNullVersion_MetricTelemetry()
        {
            // Create a Metric telemetry.
            var metricTelemetry = new MetricTelemetry("metric", 100.00);
            var initializer = new MetricSdkVersionTelemetryInitializer();

            // Assign Empty Version
            metricTelemetry.Context.GetInternalContext().SdkVersion = string.Empty;

            // Apply Initializer            
            initializer.Initialize(metricTelemetry);

            // Assign Null Version
            metricTelemetry.Context.GetInternalContext().SdkVersion = null;

            // Apply Initializer            
            initializer.Initialize(metricTelemetry);

            // Nothing to validate. If an exception was thrown, test would fail.
        }

        [Fact]
        public void Initializer_DoesNotThrowOnNull_MetricTelemetry()
        {            
            // Apply Initializer
            var initializer = new MetricSdkVersionTelemetryInitializer();
            initializer.Initialize(null);
            // Nothing to validate. If an exception was thrown, test would fail.
        }
    }
}
