// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.WebJobs.Logging.ApplicationInsights;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Loggers
{
    public class WebJobsSanitizingInitializerTests
    {
        private const string StorageString = "DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey=testkey;";
        private const string SecretReplacement = "[Hidden Credential]";

        [Fact]
        public void Sanitizes_Properties()
        {
            var initializer = new WebJobsSanitizingInitializer();

            var telemetry = new RequestTelemetry();

            // To simplify the shared projects, test sanitizer scenarios here rather than it direct unit tests.
            telemetry.Properties.Add("1", StorageString);
            telemetry.Properties.Add("2", $"Nested \"{StorageString}\" String");
            telemetry.Properties.Add("3", $"Multiple Nested \"{StorageString}\" Strings \"{StorageString}\"");
            telemetry.Properties.Add("4", $"Nested {StorageString} String");

            initializer.Initialize(telemetry);

            Assert.Equal(telemetry.Properties["1"], SecretReplacement);
            Assert.Equal(telemetry.Properties["2"], $"Nested \"{SecretReplacement}\" String");
            Assert.Equal(telemetry.Properties["3"], $"Multiple Nested \"{SecretReplacement}\" Strings \"{SecretReplacement}\"");

            // there's no terminator so the rest of the string is stripped
            Assert.Equal(telemetry.Properties["4"], $"Nested {SecretReplacement}");
        }

        [Fact]
        public void Sanitizes_Traces()
        {
            var initializer = new WebJobsSanitizingInitializer();

            var telemetry = new TraceTelemetry(StorageString);

            initializer.Initialize(telemetry);

            // Just test a simple scenario; the SanitizerTests validate others
            Assert.Equal(telemetry.Message, SecretReplacement);
        }
    }
}
