// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.DataContracts;
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
            string category)
        {
            Assert.Equal(category, request.Properties[LogConstants.CategoryNameKey]);
            Assert.Equal(LogLevel.Information.ToString(), request.Properties[LogConstants.LogLevelKey]);
            Assert.NotNull(request.Name);
            Assert.NotNull(request.Id);
            Assert.Equal(operationId, request.Context.Operation.Id);
            Assert.Equal(operationName, request.Context.Operation.Name);
            Assert.Equal(parentId, request.Context.Operation.ParentId);
            Assert.True(request.Properties.ContainsKey(LogConstants.InvocationIdKey));
            Assert.True(request.Properties.ContainsKey(LogConstants.TriggerReasonKey));
        }
    }
}
