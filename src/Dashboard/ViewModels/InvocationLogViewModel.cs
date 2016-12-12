﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dashboard.Data;
using Microsoft.Azure.WebJobs.Protocols;
using Newtonsoft.Json;

namespace Dashboard.ViewModels
{
    public class InvocationLogViewModel
    {
        internal InvocationLogViewModel(FunctionInstanceSnapshot snapshot, bool? heartbeatIsValid)
        {
            Id = snapshot.Id;
            FunctionName = snapshot.FunctionShortName;
            FunctionId = snapshot.FunctionId;
            FunctionFullName = snapshot.FunctionFullName;
            FunctionDisplayTitle = snapshot.DisplayTitle;
            HostInstanceId = snapshot.HostInstanceId;
            InstanceQueueName = snapshot.InstanceQueueName;
            if (snapshot.WebSiteName != null
                && snapshot.WebSiteName == Environment.GetEnvironmentVariable(WebSitesKnownKeyNames.WebSiteNameKey))
            {
                ExecutingJobRunId = new WebJobRunIdentifierViewModel((WebJobType)Enum.Parse(typeof(WebJobType),
                    snapshot.WebJobType), snapshot.WebJobName, snapshot.WebJobRunId);
            }

            Status = snapshot.GetStatusWithHeartbeat(heartbeatIsValid);
            
            switch (Status)
            {
                case FunctionInstanceStatus.Running:
                    WhenUtc = snapshot.StartTime.Value.UtcDateTime;
                    Duration = DateTimeOffset.UtcNow - snapshot.StartTime;
                    break;
                case FunctionInstanceStatus.CompletedSuccess:
                    WhenUtc = snapshot.EndTime.Value.UtcDateTime;
                    Duration = snapshot.GetFinalDuration();
                    break;
                case FunctionInstanceStatus.CompletedFailed:
                    WhenUtc = snapshot.EndTime.Value.UtcDateTime;
                    Duration = snapshot.GetFinalDuration();
                    ExceptionType = snapshot.ExceptionType;
                    ExceptionMessage = snapshot.ExceptionMessage;
                    break;
                case FunctionInstanceStatus.NeverFinished:
                    WhenUtc = snapshot.StartTime.Value.UtcDateTime;
                    Duration = null;
                    break;
            }
        }

        public WebJobRunIdentifierViewModel ExecutingJobRunId { get; set; }

        public Guid Id { get; set; }
        public string FunctionId { get; set; }
        public string FunctionName { get; set; }
        public string FunctionFullName { get; set; }
        public string FunctionDisplayTitle { get; set; }
        public FunctionInstanceStatus Status { get; set; }
        public DateTime? WhenUtc { get; set; }
        [JsonConverter(typeof(DurationAsMillisecondsJsonConverter))]
        public TimeSpan? Duration { get; set; }
        public string ExceptionMessage { get; set; }
        public string ExceptionType { get; set; }
        public Guid HostInstanceId { get; set; }
        public string InstanceQueueName { get; set; }
        public bool IsFinal()
        {
            return Status == FunctionInstanceStatus.CompletedFailed || Status == FunctionInstanceStatus.CompletedSuccess;
        }
    }
}
