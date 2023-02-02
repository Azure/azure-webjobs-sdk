// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Moq;
using Xunit;


namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class ApplicationInsightsLoggerProviderTests
    {
        private const string DiagnosticSourceName = ApplicationInsightsLoggerProvider.SourceName;

        public enum FlushResult
        {
            Success,
            Incomplete,
            Timeout,
            Exception
        }

        // TODO: Remove this method once an interface is implemented for Microsoft.ApplicationInsights.TelemetryClient
        // or another client is used. For now, we rely on mocking the underlying ITelemetryChannel and IAsyncFlushable
        // interfaces, which are used in the implementation of TelemetryClient.FlushAsync().
        // See https://github.com/microsoft/ApplicationInsights-dotnet/blob/main/BASE/src/Microsoft.ApplicationInsights/TelemetryClient.cs
        // for further detail.
        private TelemetryClient InitializeTestTelemetryClient(Mock<ITelemetryChannel> channel)
        {
            var config = new TelemetryConfiguration
            {
                TelemetryChannel = channel.Object,
                InstrumentationKey = "some key"
            };
            return new TelemetryClient(config);
        }

        private void SetupDelayedFlushAsync(Mock<ITelemetryChannel> mockChannel, int millisecondsTimeout, bool flushSucceeded)
        {
            mockChannel.As<IAsyncFlushable>().Setup(
                channel => channel.FlushAsync(It.IsAny<CancellationToken>()))
                    .Returns<CancellationToken>(async cancellationToken =>
                        {
                            // Delay for the set amount of time
                            await Task.Delay(millisecondsTimeout, cancellationToken);
                            return flushSucceeded;
                        });
        }

        [Theory]
        [InlineData(FlushResult.Success)]
        [InlineData(FlushResult.Incomplete)]
        [InlineData(FlushResult.Timeout)]
        [InlineData(FlushResult.Exception)]
        public void ApplicationInsightsLoggerProviderDisposal_EmitsAppropriateDiagnostics(
            FlushResult flushResult)
        {
            var mockChannel = new Mock<ITelemetryChannel>();
            var mockListener = new Mock<DiagnosticSource>();
            string expectedDiagnosticLog = "";

            switch (flushResult)
            {
                case FlushResult.Success:
                    SetupDelayedFlushAsync(mockChannel, 0, true);
                    break;
                case FlushResult.Incomplete:
                    expectedDiagnosticLog = "Flushing did not complete successfully";
                    SetupDelayedFlushAsync(mockChannel, 0, false);
                    break;
                case FlushResult.Timeout:
                    expectedDiagnosticLog = "Flushing failed. CancellationTokenSource.IsCancellationRequested: True";
                    SetupDelayedFlushAsync(mockChannel, 4000, true);
                    break;
                case FlushResult.Exception:
                    expectedDiagnosticLog = "Flushing failed. CancellationTokenSource.IsCancellationRequested: False";
                    mockChannel.As<IAsyncFlushable>().Setup(
                        channel => channel.FlushAsync(It.IsAny<CancellationToken>()))
                            .Throws(new Exception(expectedDiagnosticLog));
                    break;
                default:
                    throw new Exception("Unexpected flush result");
            }

            if (!string.IsNullOrEmpty(expectedDiagnosticLog))
            {
                mockListener.Setup(listener =>
                    listener.IsEnabled(DiagnosticSourceName)).Returns(true);
                mockListener.Setup(listener =>
                    listener.Write(DiagnosticSourceName, It.IsAny<string>()));
            }

            var client = InitializeTestTelemetryClient(mockChannel);
            var loggerProvider = new ApplicationInsightsLoggerProvider(client, mockListener.Object);
            loggerProvider.Dispose();

            if (flushResult == FlushResult.Success)
            {
                // No diagnostic logs should be emitted
                mockListener.Verify(listener => listener.IsEnabled(DiagnosticSourceName), Times.Never);
            }
            else
            {
                // There should only be one check for each diagnostic log emitted
                mockListener.Verify(listener => listener.IsEnabled(DiagnosticSourceName), Times.Once);
                mockListener.Verify(listener => listener.Write(DiagnosticSourceName, expectedDiagnosticLog), Times.Once);
            }
        }
    }
}
