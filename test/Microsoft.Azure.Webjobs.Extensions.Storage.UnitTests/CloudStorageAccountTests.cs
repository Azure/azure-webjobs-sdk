// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Blobs;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests
{
    public class CloudStorageAccountTests
    {
        [Fact]
        public void CloudStorageAccount_CanCall()
        {
            // Arrange
            CloudStorageAccount realAccount = CloudStorageAccount.DevelopmentStorageAccount;

            XStorageAccount account = XStorageAccount.New(realAccount);

            // Act
            var t = typeof(CloudStorageAccountProgram);
            var result = FunctionalTest.Call<CloudStorageAccount>(
                account, 
                t, 
                t.GetMethod(nameof(CloudStorageAccountProgram.BindToCloudStorageAccount)), 
                null, // args
                (s) => CloudStorageAccountProgram.TaskSource = s);

            // Assert
            Assert.NotNull(result);
            Assert.Same(realAccount, result);
        }

        private class CloudStorageAccountProgram
        {
            public static TaskCompletionSource<CloudStorageAccount> TaskSource { get; set; }

            [NoAutomaticTrigger]
            public static void BindToCloudStorageAccount(CloudStorageAccount account)
            {
                TaskSource.TrySetResult(account);
            }
        }
    }
}
