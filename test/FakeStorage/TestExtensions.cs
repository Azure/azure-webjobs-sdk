// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace FakeStorage
{
    // $$$
    public static class TestExtensions
    {
        // Internal Ctor 
        public static CloudBlobDirectory NewCloudBlobDirectory(StorageUri uri, string prefix, CloudBlobContainer container)
        {
            var ctor = typeof(CloudBlobDirectory).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                new Type[] { typeof(StorageUri), typeof(string), typeof(CloudBlobContainer) },
                null);

            var result = ctor.Invoke(new object[] { uri, prefix, container });
            return (CloudBlobDirectory) result;
        }

        public static T SetInternalField<T>(this T obj, string name, object value)
        {
            var t = obj.GetType();
            
            var prop = t.GetProperty(name,
              BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (!prop.CanWrite)
            {
                t = t.BaseType;
                prop = t.GetProperty(name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }

            prop.SetValue(obj, value);
            return obj;
        }

        public static BlobProperties SetEtag(this BlobProperties props, string etag )
        {
            props.SetInternalField(nameof(BlobProperties.ETag), etag);
            return props;
        }

        public static BlobResultSegment NewBlobResultSegment(
            BlobContinuationToken continuationToken,
            IEnumerable<ICloudBlob> results
            )
        {
            throw new NotImplementedException();
        }

        public static CloudQueueMessage SetDequeueCount(this CloudQueueMessage msg, int value)
        {
            msg.SetInternalField(nameof(CloudQueueMessage.DequeueCount), value);
            return msg;
        }

        public static CloudQueueMessage SetExpirationTime(this CloudQueueMessage msg, DateTimeOffset? value)
        {
            msg.SetInternalField(nameof(CloudQueueMessage.ExpirationTime), value);
            return msg;
        }

        public static CloudQueueMessage SetId(this CloudQueueMessage msg, string value)
        {
            msg.SetInternalField(nameof(CloudQueueMessage.Id), value);
            return msg;
        }

        public static CloudQueueMessage SetInsertionTime(this CloudQueueMessage msg, DateTimeOffset? value)
        {
            msg.SetInternalField(nameof(CloudQueueMessage.InsertionTime), value);
            return msg;
        }

        public static CloudQueueMessage SetNextVisibleTime(this CloudQueueMessage msg, DateTimeOffset? value)
        {
            msg.SetInternalField(nameof(CloudQueueMessage.NextVisibleTime), value);
            return msg;
        }

        public static CloudQueueMessage SetPopReceipt(this CloudQueueMessage msg, string value)
        {
            msg.SetInternalField(nameof(CloudQueueMessage.PopReceipt), value);
            return msg;
        }
    }

    static class MoreStorageExtensions
    {
        public static string DownloadText(this ICloudBlob blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException("blob");
            }

            using (Stream stream = blob.OpenReadAsync(null, null, null).GetAwaiter().GetResult())
            using (TextReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public static async Task UploadEmptyPageAsync(this CloudPageBlob blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException("blob");
            }

            using (CloudBlobStream stream = await blob.OpenWriteAsync(512))
            {
                await stream.CommitAsync();
            }
        }
    }
}
