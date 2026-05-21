// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal static class LoggingConstants
    {
        public const string ZeroIpAddress = "0.0.0.0";
        public const string Unknown = "[Unknown]";
        public const string ClientIpKey = "ClientIp";
        public const string HostInstanceIdKey = "HostInstanceId";

        // Property added by the Application Insights SDK metric aggregation pipeline
        // (e.g. when metrics are emitted via TelemetryClient.GetMetric(...).TrackValue(...)).
        // This dimension is no longer published on custom metrics once migrated to the
        // OpenTelemetry exporter, so it is stripped when EnableMetricsCustomDimensionOptimization
        // is enabled to keep behavior consistent across exporters.
        public const string AggregationIntervalMsKey = "_MS.AggregationIntervalMs";
    }
}
