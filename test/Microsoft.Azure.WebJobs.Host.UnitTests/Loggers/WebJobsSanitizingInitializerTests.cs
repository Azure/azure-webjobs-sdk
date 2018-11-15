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
        private const string AssemblyLoadErrorWithAllowedToken = "System.AggregateException: aggregate error --->System.IO.FileNotFoundException: Could not load file or assembly 'Microsoft.Azure.WebJobs.Host, Version=2.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35' or one of its dependencies.The system cannot find the file specified.at System.Reflection.RuntimeAssembly._nLoad(AssemblyName fileName, String codeBase, Evidence assemblySecurity, RuntimeAssembly locationHint, StackCrawlMark & stackMark, IntPtr pPrivHostBinder, Boolean throwOnFileNotFound, Boolean forIntrospection, Boolean suppressSecurityChecks)";
        private const string AssemblyLoadError = "System.AggregateException: aggregate error --->System.IO.FileNotFoundException: Could not load file or assembly 'Microsoft.Azure.WebJobs.Host, Version=2.0.0.0, Culture=neutral, Token=31bf3856ad364e35' or one of its dependencies.The system cannot find the file specified.at System.Reflection.RuntimeAssembly._nLoad(AssemblyName fileName, String codeBase, Evidence assemblySecurity, RuntimeAssembly locationHint, StackCrawlMark & stackMark, IntPtr pPrivHostBinder, Boolean throwOnFileNotFound, Boolean forIntrospection, Boolean suppressSecurityChecks)";
        private const string AssemblyLoadErrorSanitized = "System.AggregateException: aggregate error --->System.IO.FileNotFoundException: Could not load file or assembly 'Microsoft.Azure.WebJobs.Host, Version=2.0.0.0, Culture=neutral, [Hidden Credential]' or one of its dependencies.The system cannot find the file specified.at System.Reflection.RuntimeAssembly._nLoad(AssemblyName fileName, String codeBase, Evidence assemblySecurity, RuntimeAssembly locationHint, StackCrawlMark & stackMark, IntPtr pPrivHostBinder, Boolean throwOnFileNotFound, Boolean forIntrospection, Boolean suppressSecurityChecks)";
        private const string TestStrigWithAllowedTokenAndSecretToken = "Test String \"PublicKeyToken=31bf3856ad364e35 Token=31bf3856ad364e35\"";
        private const string TestStrigWithAllowedTokenAndSecretTokenSanitized = "Test String \"PublicKeyToken=31bf3856ad364e35 [Hidden Credential]\"";

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
            telemetry.Properties.Add("5", AssemblyLoadErrorWithAllowedToken);
            telemetry.Properties.Add("6", AssemblyLoadError);
            telemetry.Properties.Add("7", TestStrigWithAllowedTokenAndSecretToken);

            // Run it through twice to ensure idempotency
            initializer.Initialize(telemetry);
            initializer.Initialize(telemetry);

            Assert.Equal(telemetry.Properties["1"], SecretReplacement);
            Assert.Equal(telemetry.Properties["2"], $"Nested \"{SecretReplacement}\" String");
            Assert.Equal(telemetry.Properties["3"], $"Multiple Nested \"{SecretReplacement}\" Strings \"{SecretReplacement}\"");

            // there's no terminator so the rest of the string is stripped
            Assert.Equal(telemetry.Properties["4"], $"Nested {SecretReplacement}");

            // No secrets in the string. Keep the original
            Assert.Equal(telemetry.Properties["5"], AssemblyLoadErrorWithAllowedToken);

            Assert.Equal(telemetry.Properties["6"], AssemblyLoadErrorSanitized);
            Assert.Equal(telemetry.Properties["7"], TestStrigWithAllowedTokenAndSecretTokenSanitized);
        }

        [Fact]
        public void Sanitizes_Traces()
        {
            var initializer = new WebJobsSanitizingInitializer();

            var telemetry = new TraceTelemetry(StorageString);

            // Run it through twice to ensure idempotency
            initializer.Initialize(telemetry);
            initializer.Initialize(telemetry);

            // Just test a simple scenario; the SanitizerTests validate others
            Assert.Equal(telemetry.Message, SecretReplacement);
        }
    }
}