// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class WebJobsTelemetryInitializerTests : IDisposable
    {
        // App Insights performs auto-tracking by watching DiagnosticSources 
        // from the .NET Framework. During host restarts in Functions, it's 
        // possible that we have two TelemetryClients running simultaneously in
        // the same process (one stopping; one starting). This means the same 
        // Telemetry may go through the initializer twice. These tests verify
        // the trickier scenarios.

        [Fact]
        public void Initializer_IsIdempotent_HttpRequest()
        {
            string functionName = "MyFunction";
            string status = "409";
            Uri uri = new Uri("http://localhost/api/somemethod?secret=secret");

            // Simulate an HttpRequest
            var request = new RequestTelemetry
            {
                Url = uri,
                ResponseCode = status,
                Name = "POST /api/somemethod",
            };
            
            var options = new ApplicationInsightsLoggerOptions();

            Activity requestActivity = new Activity("dummy");
            requestActivity.Start();
            requestActivity.AddTag(LogConstants.NameKey, functionName);
            requestActivity.AddTag(LogConstants.SucceededKey, "true");

            var initializer = new WebJobsTelemetryInitializer(new WebJobsSdkVersionProvider(), new WebJobsRoleInstanceProvider(), Options.Create(options));

            initializer.Initialize(request);
            initializer.Initialize(request);

            Assert.Equal(functionName, request.Context.Operation.Name);
            Assert.Equal(status, request.ResponseCode);
            Assert.Equal(true, request.Success);
            Assert.Equal(functionName, request.Name);

            Assert.True(request.Properties.TryGetValue(LogConstants.HttpMethodKey, out var actualHttpMethod));
            Assert.Equal("POST", actualHttpMethod);

            Assert.True(request.Properties.TryGetValue(LogConstants.HttpPathKey, out var actualHttpPath));
            Assert.Equal("/api/somemethod", actualHttpPath);

            Assert.DoesNotContain(request.Properties, p => p.Key == LogConstants.SucceededKey);
        }

        [Fact]
        public void Initializer_IsIdempotent_ServiceBusRequest()
        {
            string functionName = "MyFunction";

            // Simulate an HttpRequest
            var request = new RequestTelemetry
            {
                ResponseCode = string.Empty,
                Name = "",
                Url = new Uri("http://localhost/api/somemethod?secret=secret")
        };

            Activity requestActivity = new Activity("dummy");
            requestActivity.Start();
            requestActivity.AddTag(LogConstants.NameKey, functionName);
            requestActivity.AddTag(LogConstants.SucceededKey, "true");

            var options = new ApplicationInsightsLoggerOptions();
            var initializer = new WebJobsTelemetryInitializer(new WebJobsSdkVersionProvider(), new WebJobsRoleInstanceProvider(), Options.Create(options));

            initializer.Initialize(request);
            initializer.Initialize(request);

            Assert.Equal(functionName, request.Name);
            Assert.Equal("0", request.ResponseCode);
            Assert.Equal(true, request.Success);
            Assert.Equal(request.Url, new Uri("http://localhost/api/somemethod")); 
            Assert.DoesNotContain(request.Properties, p => p.Key == LogConstants.SucceededKey);
        }


        [Fact]
        public void Initializer_IsIdempotent_ServiceBusRequest_EnableQueryStringTracing()
        {
            string functionName = "MyFunction";

            // Simulate an HttpRequest
            var request = new RequestTelemetry
            {
                ResponseCode = string.Empty,
                Name = "",
                Url = new Uri("http://localhost/api/somemethod?secret=secret")
            };

            Activity requestActivity = new Activity("dummy");
            requestActivity.Start();
            requestActivity.AddTag(LogConstants.NameKey, functionName);
            requestActivity.AddTag(LogConstants.SucceededKey, "true");

            var options = new ApplicationInsightsLoggerOptions();
            IOptions<ApplicationInsightsLoggerOptions> aiOptions = Options.Create(options);
            aiOptions.Value.EnableQueryStringTracing = true;
            var initializer = new WebJobsTelemetryInitializer(new WebJobsSdkVersionProvider(), new WebJobsRoleInstanceProvider(), aiOptions);

            initializer.Initialize(request);
            initializer.Initialize(request);

            Assert.Equal(functionName, request.Name);
            Assert.Equal("0", request.ResponseCode);
            Assert.Equal(true, request.Success);
            Assert.Equal(request.Url, new Uri("http://localhost/api/somemethod?secret=secret"));
            Assert.DoesNotContain(request.Properties, p => p.Key == LogConstants.SucceededKey);
        }

        [Fact]
        public void Initializer_SetsRoleInstance()
        {
            var request = new RequestTelemetry { Name = "custom" };
            var options = new ApplicationInsightsLoggerOptions();
            var initializer = new WebJobsTelemetryInitializer(new WebJobsSdkVersionProvider(), new TestRoleInstanceProvider("my custom role instance"), Options.Create(options));

            initializer.Initialize(request);

            Assert.Equal("my custom role instance", request.Context.Cloud.RoleInstance);
        }

        public void Dispose()
        {
            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
        }

        class TestRoleInstanceProvider : IRoleInstanceProvider
        {
            private readonly string _roleInstance;

            public TestRoleInstanceProvider(string roleInstance)
            {
                _roleInstance = roleInstance;
            }

            public string GetRoleInstanceName()
            {
                return _roleInstance;
            }
        }
    }
}
