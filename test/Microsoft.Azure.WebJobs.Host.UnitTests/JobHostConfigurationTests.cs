﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class JobHostConfigurationTests
    {
        [Fact]
        public void HostId_IfNull_DoesNotThrow()
        {
            // Arrange
            JobHostConfiguration configuration = new JobHostConfiguration();
            string hostId = null;

            // Act & Assert
            Assert.DoesNotThrow(() => { configuration.HostId = hostId; });
        }

        [Fact]
        public void HostId_IfValid_DoesNotThrow()
        {
            // Arrange
            JobHostConfiguration configuration = new JobHostConfiguration();
            string hostId = "abc";

            // Act & Assert
            Assert.DoesNotThrow(() => { configuration.HostId = hostId; });
        }

        [Fact]
        public void HostId_IfMinimumLength_DoesNotThrow()
        {
            // Arrange
            JobHostConfiguration configuration = new JobHostConfiguration();
            string hostId = "a";

            // Act & Assert
            Assert.DoesNotThrow(() => { configuration.HostId = hostId; });
        }

        [Fact]
        public void HostId_IfMaximumLength_DoesNotThrow()
        {
            // Arrange
            JobHostConfiguration configuration = new JobHostConfiguration();
            const int maximumValidCharacters = 32;
            string hostId = new string('a', maximumValidCharacters);

            // Act & Assert
            Assert.DoesNotThrow(() => { configuration.HostId = hostId; });
        }

        [Fact]
        public void HostId_IfContainsEveryValidLetter_DoesNotThrow()
        {
            // Arrange
            JobHostConfiguration configuration = new JobHostConfiguration();
            string hostId = "abcdefghijklmnopqrstuvwxyz";

            // Act & Assert
            Assert.DoesNotThrow(() => { configuration.HostId = hostId; });
        }

        [Fact]
        public void HostId_IfContainsEveryValidOtherCharacter_DoesNotThrow()
        {
            // Arrange
            JobHostConfiguration configuration = new JobHostConfiguration();
            string hostId = "0-123456789";

            // Act & Assert
            Assert.DoesNotThrow(() => { configuration.HostId = hostId; });
        }

        [Fact]
        public void HostId_IfEmpty_Throws()
        {
            TestHostIdThrows(String.Empty);
        }

        [Fact]
        public void HostId_IfTooLong_Throws()
        {
            const int maximumValidCharacters = 32;
            string hostId = new string('a', maximumValidCharacters + 1);
            TestHostIdThrows(hostId);
        }

        [Fact]
        public void HostId_IfContainsInvalidCharacter_Throws()
        {
            // Uppercase character are not allowed.
            TestHostIdThrows("aBc");
        }

        [Fact]
        public void HostId_IfStartsWithDash_Throws()
        {
            TestHostIdThrows("-abc");
        }

        [Fact]
        public void HostId_IfEndsWithDash_Throws()
        {
            TestHostIdThrows("abc-");
        }

        [Fact]
        public void HostId_IfContainsConsecutiveDashes_Throws()
        {
            TestHostIdThrows("a--bc");
        }

        [Fact]
        public void JobActivator_IfNull_Throws()
        {
            JobHostConfiguration configuration = new JobHostConfiguration();

            ExceptionAssert.ThrowsArgumentNull(() => configuration.JobActivator = null, "value");
        }

        [Fact]
        public void GetService_ReturnsExpectedDefaultServices()
        {
            JobHostConfiguration configuration = new JobHostConfiguration();

            IExtensionRegistry extensionRegistry = configuration.GetService<IExtensionRegistry>();
            extensionRegistry.RegisterExtension<IComparable>("test1");
            extensionRegistry.RegisterExtension<IComparable>("test2");
            extensionRegistry.RegisterExtension<IComparable>("test3");

            Assert.NotNull(extensionRegistry);
            IComparable[] results = extensionRegistry.GetExtensions<IComparable>().ToArray();
            Assert.Equal(3, results.Length);

            IJobHostContextFactory jobHostContextFactory = configuration.GetService<IJobHostContextFactory>();
            Assert.NotNull(jobHostContextFactory);
        }

        [Fact]
        public void GetService_ThrowsArgumentNull_WhenServiceTypeIsNull()
        {
            JobHostConfiguration configuration = new JobHostConfiguration();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => configuration.GetService(null)
            );
            Assert.Equal("serviceType", exception.ParamName);
        }

        [Fact]
        public void GetService_ReturnsNull_WhenServiceTypeNotFound()
        {
            JobHostConfiguration configuration = new JobHostConfiguration();

            object result = configuration.GetService(typeof(IComparable));
            Assert.Null(result);
        }

        [Fact]
        public void AddService_AddsNewService()
        {
            JobHostConfiguration configuration = new JobHostConfiguration();

            IComparable service = "test1";
            configuration.AddService<IComparable>(service);

            IComparable result = configuration.GetService<IComparable>();
            Assert.Same(service, result);
        }

        [Fact]
        public void AddService_ReplacesExistingService()
        {
            JobHostConfiguration configuration = new JobHostConfiguration();

            IComparable service = "test1";
            configuration.AddService<IComparable>(service);

            IComparable result = configuration.GetService<IComparable>();
            Assert.Same(service, result);

            IComparable service2 = "test2";
            configuration.AddService<IComparable>(service2);
            result = configuration.GetService<IComparable>();
            Assert.Same(service2, result);
        }

        [Fact]
        public void AddService_ThrowsArgumentNull_WhenServiceTypeIsNull()
        {
            JobHostConfiguration configuration = new JobHostConfiguration();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => configuration.AddService(null, "test1")
            );
            Assert.Equal("serviceType", exception.ParamName);
        }

        [Fact]
        public void AddService_ThrowsArgumentOutOfRange_WhenInstanceNotInstanceOfType()
        {
            JobHostConfiguration configuration = new JobHostConfiguration();

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => configuration.AddService(typeof(IComparable), new object())
            );
            Assert.Equal("serviceInstance", exception.ParamName);
        }

        private static void TestHostIdThrows(string hostId)
        {
            // Arrange
            JobHostConfiguration configuration = new JobHostConfiguration();

            // Act & Assert
            ExceptionAssert.ThrowsArgument(() => { configuration.HostId = hostId; }, "value",
                "A host ID must be between 1 and 32 characters, contain only lowercase letters, numbers, and " +
                "dashes, not start or end with a dash, and not contain consecutive dashes.");
        }
    }
}
