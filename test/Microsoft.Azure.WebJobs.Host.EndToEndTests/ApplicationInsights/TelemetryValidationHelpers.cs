// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.EndToEndTests.ApplicationInsights
{
    internal class TelemetryValidationHelpers
    {
        public static void ValidateHttpDependency(
            DependencyTelemetry dependency,
            string operationName,
            string operationId,
            string parentId,
            string category)
        {
            Assert.False(string.IsNullOrEmpty(dependency.ResultCode));
            Assert.NotNull(dependency.Data);

            ValidateDependency(dependency, operationName, operationId, parentId, category);
        }

        public static void ValidateDependency(
            DependencyTelemetry dependency,
            string operationName,
            string operationId,
            string parentId,
            string category)
        {
            Assert.Equal(category, dependency.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Information.ToString(), dependency.Properties[LogConstants.LogLevelKey]);
            Assert.NotNull(dependency.Target);
            Assert.NotNull(dependency.Name);
            Assert.NotNull(dependency.Id);
            Assert.Equal(operationId, dependency.Context.Operation.Id);
            Assert.Equal(operationName, dependency.Context.Operation.Name);
            Assert.Equal(parentId, dependency.Context.Operation.ParentId);
            Assert.True(dependency.Properties.ContainsKey(LogConstants.InvocationIdKey));
        }

        public static void ValidateRequest(
            RequestTelemetry request,
            string operationName,
            string operationId,
            string parentId,
            string category,
            LogLevel logLevel = LogLevel.Information,
            bool success = true,
            string statusCode = "0",
            string httpMethod = null)

        {
            Assert.Equal(category, request.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(logLevel.ToString(), request.Properties[LogConstants.LogLevelKey]);
            Assert.NotNull(request.Name);
            Assert.NotNull(request.Id);

            if (operationId != null)
            {
                Assert.Equal(operationId, request.Context.Operation.Id);
            }

            if (parentId != null)
            {
                Assert.Equal(parentId, request.Context.Operation.ParentId);
            }
            Assert.Equal(operationName, request.Context.Operation.Name);
            Assert.True(request.Properties.ContainsKey(LogConstants.InvocationIdKey));
            Assert.True(request.Properties.ContainsKey(LogConstants.TriggerReasonKey));
            Assert.StartsWith("webjobs:", request.Context.GetInternalContext().SdkVersion);

            Assert.Equal(success, request.Success);
            Assert.Equal(statusCode, request.ResponseCode);

            Assert.DoesNotContain(request.Properties, p => p.Key == LogConstants.SucceededKey);

            if (httpMethod != null)
            {
                Assert.Equal(httpMethod, request.Properties[LogConstants.HttpMethodKey]);
            }
            else
            {
                Assert.DoesNotContain(request.Properties, p => p.Key == LogConstants.HttpMethodKey);
            }
        }
    }
}