using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Jobs.Host.TestCommon;
using Microsoft.Azure.Jobs.ServiceBus.Listeners;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Moq;
using Xunit;

namespace Microsoft.Azure.Jobs.ServiceBus.UnitTests.Listeners
{
    public class NamespaceManagerExtensionsTests
    {
        const string TestEntityPath = "long/fake/path";

        [Fact]
        public void SplitQueuePath_IfNonDLQPath_ReturnsOriginalPath()
        {
            string[] result = NamespaceManagerExtensions.SplitQueuePath(TestEntityPath);

            Assert.NotNull(result);
            Assert.Equal(1, result.Length);
            Assert.Equal(TestEntityPath, result[0]);
        }

        [Fact]
        public void SplitQueuePath_IfDLQPath_ReturnsPathToParentEntity()
        {
            string path = QueueClient.FormatDeadLetterPath(TestEntityPath);

            string[] result = NamespaceManagerExtensions.SplitQueuePath(path);

            Assert.NotNull(result);
            Assert.Equal(2, result.Length);
            Assert.Equal(TestEntityPath, result[0]);
        }

        [Fact]
        public void SplitQueuePath_IfNullArgument_Throws()
        {
            string path = null;

            ExceptionAssert.ThrowsArgument(() => NamespaceManagerExtensions.SplitQueuePath(path),
                "path", "path cannot be null or empty");
        }

        [Fact]
        public void SplitQueuePath_IfPathIsEmpty_Throws()
        {
            string path = "";

            ExceptionAssert.ThrowsArgument(() => NamespaceManagerExtensions.SplitQueuePath(path),
                "path", "path cannot be null or empty");
        }
    }
}
