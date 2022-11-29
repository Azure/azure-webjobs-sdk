// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Trace
{
    internal static class TraceConstants
    {
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/faas.md
        public const string FunctionsActivitySource = "Microsoft.Azure.WebJobs.Host";//"Microsoft.Azure.Functions.Host";
        public const string FunctionsActivitySourceVersion = "1.0.0.0";
        public const string FunctionsActivityName = "Execute";




        public const string AttributeFaasTime = "faas.time";
        public const string AttributeFaasCron = "faas.cron";

        public const string AttributeExceptionEventName = "exception";
        public const string AttributeExceptionType = "exception.type";
        public const string AttributeExceptionMessage = "exception.message";
        public const string AttributeExceptionStacktrace = "exception.stacktrace";

        public const string StatusCodeKey = "otel.status_code";
        public const string StatusDescriptionKey = "otel.status_description";

        public const string OkStatusCodeTagValue = "OK";
        public const string ErrorStatusCodeTagValue = "ERROR";

        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/semantic_conventions/cloud.md
        // Experimental -- should be behind experimental flag before GA
        // Resources
        // cloud
        public const string AttributeCloudProviderKey = "cloud.provider";
        public const string AttributeCloudProviderValue = "azure";

        public const string AttributeCloudPlatformKey = "cloud.platform";
        public const string AttributeCloudPlatformValue = "azure_functions";

        public const string AttributeCloudRegionKey = "cloud.region";

        //https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/semantic_conventions/faas.md
        //faas
        public const string AttributeFaasNameKey = "faas.name"; // <FUNCAPP>/<FUNC>
        public const string AttributeFaasIdKey = "faas.id"; // /subscriptions/<SUBSCIPTION_GUID>/resourceGroups/<RG>/providers/Microsoft.Web/sites/<FUNCAPP>/functions/<FUNC>



        // General attributes
        //Required tag, tt SHOULD be one of the following strings: "datasource", "http", "pubsub", "timer", or "other".
        public const string AttributeFaasTrigger = "faas.trigger"; // http

        // current request ID handled by the function - invocationId
        public const string AttributeFaasExecution = "faas.execution";

        // current invocation ID handled by the function - instanceId
        public const string AttributeFaasInstanceKey = "faas.instance";

        public const string AttributeFaasColdStart = "faas.coldstart";

        // Trigger type - datasource
        // Required tag
        public const string AttributeFaasDocumentCollection = "faas.document.collection";
        // Required, time when the data was accessed.
        public const string AttributeFaasDocumentOperation = "faas.document.operation";
        public const string AttributeFaasDocumentTime = "faas.document.time";
        public const string AttributeFaasDocumentName = "faas.document.name";

        // Trigger type - http
        //"http.server_name"
        //"http.route"
        //"http.url"
    }
}