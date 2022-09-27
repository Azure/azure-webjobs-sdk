// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    /// <summary>
    /// Transmission Status Telemetry Module 
    /// </summary>
    internal class TransmissionStatusTelemetryModule : ITelemetryModule, IDisposable
    {
        private bool isInitialized = false;
        DiagnosticListener source = new DiagnosticListener(LoggingConstants.HostDiagnosticSourcePrefix + "ApplicationInsights");
        string debugTracingEventName = LoggingConstants.HostDiagnosticSourceDebugEventNamePrefix + "TransmissionStatus";
        JsonSerializerSettings options = new JsonSerializerSettings() { Error = (sender, error) => error.ErrorContext.Handled = true, NullValueHandling = NullValueHandling.Ignore };


        /// <summary>
        /// Initializes the telemetry module.
        /// </summary>
        /// <param name="configuration">Telemetry configuration to use for initialization.</param>
        public void Initialize(TelemetryConfiguration configuration)
        {
            // Prevent the telemetry module from being initialized multiple times.
            if (isInitialized)
            {
                return;
            }
            (configuration.TelemetryChannel as ServerTelemetryChannel).TransmissionStatusEvent += Handler;
            isInitialized = true;
        }

        /// <summary>
        /// Disposes the object.
        /// </summary>
        public void Dispose()
        {
            source.Dispose();
        }

        public void Handler(object sender, TransmissionStatusEventArgs args)
        {
            // Do not block the main thread
            Task.Run(() =>
            {
                if (sender != null && args != null)
                {
                    // Always log if the response is non-success
                    if (args.Response.StatusCode != 200)
                    {
                        var transmission = sender as Transmission;
                        string id = null;
                        if (transmission != null)
                        {                        
                            id = transmission.Id;
                        }

                        var log = new
                        {
                            statusCode = args.Response?.StatusCode,
                            description = args.Response?.StatusDescription,
                            id
                        };
                        source.Write("TransmissionStatus", JsonConvert.SerializeObject(log, options));
                    }

                    // Log everything if the debug trace feature flag is enabled
                    if (source.IsEnabled(debugTracingEventName))
                    {
                        source.Write(debugTracingEventName, FormattedLog(sender, args));
                    }
                }
            });
        }
        internal string FormattedLog(object sender, TransmissionStatusEventArgs args)
        {
            var transmission = sender as Transmission;
            if (transmission != null)
            {
                var items = transmission.TelemetryItems.GroupBy(n => n.GetEnvelopeName())
                            .Select(n => new
                            {
                                type = n.Key,
                                count = n.Count()
                            });

                BackendResponse backendResponse = null;
                if (!string.IsNullOrWhiteSpace(args.Response?.Content))
                {
                    backendResponse = JsonConvert.DeserializeObject<BackendResponse>(args.Response.Content, options);
                }
                string topErrorMessage = null;
                int? topStatusCode = null;
                if (backendResponse?.Errors != null && backendResponse.Errors.Count() > 0)
                {
                    topErrorMessage = backendResponse.Errors[0].Message;
                    topStatusCode = backendResponse?.Errors[0].StatusCode;
                }

                var log = new
                {
                    items = items,
                    statusCode = args.Response?.StatusCode,
                    statusDescription = args.Response?.StatusDescription,
                    retryAfterHeader = args.Response?.RetryAfterHeader,
                    id = transmission.Id,
                    timeout = transmission.Timeout,
                    responseTimeInMs = args.ResponseDurationInMs,
                    received = backendResponse?.ItemsReceived,
                    accepted = backendResponse?.ItemsAccepted,
                    errorMessage = topErrorMessage,
                    errorCode = topStatusCode,
                };
                return JsonConvert.SerializeObject(log, options);
            }
            return "Unable to parse transmission status response";
        }
    }

    internal class BackendResponse
    {
        [JsonProperty("itemsReceived")]
        public int ItemsReceived { get; set; }

        [JsonProperty("itemsAccepted")]
        public int ItemsAccepted { get; set; }

        [JsonProperty("errors")]
        public Error[] Errors { get; set; }
        public class Error
        {

            [JsonProperty("statusCode")]
            public int StatusCode { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }
        }
    }
}