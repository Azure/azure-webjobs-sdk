// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    public class WebJobsExtensionOptionRegistry 
    {
        public static bool OptIn { get; set; } = false;

        private static IWebJobsExtensionOptionDataSource _instance = new WebJobsExtensionOptionDataSource();

        private static ConcurrentDictionary<string, Action> _subscriber = new ConcurrentDictionary<string, Action>();

        /// <summary>
        /// Interface for testability. Do not use in Production.
        /// </summary>
        public static IWebJobsExtensionOptionDataSource Instance
        {
            set
            {
                _instance = value;
            }
        }

        /// <summary>
        /// Register the options object of an WebJobs extension in Memory.
        /// </summary>
        /// <param name="section"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static object Register(string section, object options)
        {
            var obj = _instance.Register(section, options);
            Nortify();
            return obj;
        }

        /// <summary>
        /// Subscribe the notification of the change.
        /// </summary>
        /// <param name="key">Unique string</param>
        /// <param name="action">Action that you want to execute when receives the change</param>
        /// <returns></returns>
        public static object Subscribe(string key, Action action)
        {
            return _subscriber.AddOrUpdate(key, action, (k, v) => action);
        }

        private static void Nortify()
        {
            foreach (var x in _subscriber.Values)
            {
                x.Invoke();
            }
        }

        /// <summary>
        /// Clear the in memory registration.
        /// </summary>
        public static void Clear()
        {
            _instance.Clear();
        }
        
        /// <summary>
        /// Get the registered options.
        /// </summary>
        /// <returns></returns>
        public static JObject GetOptions()
        {
            return _instance.GetOptions();
        }
    }
}
