// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Host.Tracing
{
    internal static class TraceConstants
    {
        // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/faas.md
        public const string FunctionsActivitySource = "Microsoft.Azure.WebJobs.Host";
        public const string FunctionsActivityName = "Execute";


        
        public const string AttributeFaasDocumentCollection = "faas.document.collection";
        public const string AttributeFaasDocumentOperation = "faas.document.operation";
        public const string AttributeFaasDocumentTime = "faas.document.time";
        public const string AttributeFaasDocumentName = "faas.document.name";
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
        public const string AttributeFaasInstanceKey = "faas.instance"; // instanceId

        // Span
        public const string AttributeFaasExecution = "faas.execution"; // invocationId
        public const string AttributeFaasColdStart = "faas.coldstart"; // true
        public const string AttributeFaasTrigger = "faas.trigger"; // http
    }
}
