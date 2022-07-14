// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
using System;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Azure.WebJobs.Host.Tracing
{
    /// <summary>
    /// Extension methods for the <see cref="Activity"/> class.
    /// </summary>
    public static class ActivityExtentions
    {
        public static void RecordException(this Activity activity, Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            var tagsCollection = new ActivityTagsCollection
            {
                { TraceConstants.AttributeExceptionType, ex.GetType().FullName },
                { TraceConstants.AttributeExceptionStacktrace, ex.ToString() }
            };

            if (!string.IsNullOrWhiteSpace(ex.Message))
            {
                tagsCollection.Add(TraceConstants.AttributeExceptionMessage, ex.Message);
            }

            activity?.SetTag(TraceConstants.StatusCodeKey, "ERROR");
            activity?.AddEvent(new ActivityEvent(TraceConstants.AttributeExceptionEventName, default, tagsCollection));
        }

        public static void SetStatus(this Activity activity, string statusCode, string description)
        {
            activity?.SetTag(TraceConstants.StatusCodeKey, statusCode);
            activity?.SetTag(TraceConstants.StatusDescriptionKey, description);
        }
    }
}
