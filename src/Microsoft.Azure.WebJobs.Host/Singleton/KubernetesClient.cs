// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Singleton;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class KubernetesClient
    {
        private HttpClient _httpClient;

        public KubernetesClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<LockResponse> GetLock(string lockName)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            request.RequestUri = GetRequestUri($"?name={lockName}");
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<LockResponse>(responseString);
            }
            return new LockResponse() { Owner = null };
        }

        public async Task<KubernetesLockHandle> TryAcquireLock (string lockId, string ownerId, string lockPeriod)
        {
            var lockHandle = new KubernetesLockHandle();
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = GetRequestUri($"/acquire?name={lockId}&owner={ownerId}&period={lockPeriod}&renewDeadline=10"),
            };

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                lockHandle.LockId = lockId;
                lockHandle.OwnerId = ownerId;
            }
            return lockHandle;
        }

        public async Task<HttpResponseMessage> ReleaseLock(string lockId, string ownerId)
        {
            var lockHandle = new KubernetesLockHandle();
            var request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = GetRequestUri($"/release?name={lockId}&owner={ownerId}")
            };

            return await _httpClient.SendAsync(request);
        }

        private Uri GetRequestUri(string requestStem)
        {
            return new Uri($"http://localhost:21000/lock{requestStem}");
        }
    }
}
