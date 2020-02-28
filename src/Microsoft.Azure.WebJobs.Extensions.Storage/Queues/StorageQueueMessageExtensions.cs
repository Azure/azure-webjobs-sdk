// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.Azure.Storage.Queue;

namespace Microsoft.Azure.WebJobs.Host.Queues
{
    internal static class StorageQueueMessageExtensions
    {
        public static string TryGetAsString(this CloudQueueMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            try
            {
                string value = message.AsString;
                return value;
            }
            catch (Exception ex)
            {
                if (! (ex is DecoderFallbackException || ex is FormatException))
                {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }

                return null;
            }
        }
    }
}
