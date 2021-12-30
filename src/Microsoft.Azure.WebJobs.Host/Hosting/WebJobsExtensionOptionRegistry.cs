// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Hosting
{
    public class WebJobsExtensionOptionRegistry 
    {
        public const string ConcurrencySectionName = "Concurrency";
        public const string ExtensionsSectionName = "Extensions";

        public static bool OptIn { get; set; } = false;

        private static IWebJobsExtensionOptionDataSource _instance = new WebJobsExtensionOptionDataSource();
        
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
        /// <param name="subSection"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static object Register(string section, string subSection, object options)
        {
            return _instance.Register(section, subSection, options);
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
        /// <param name="section"></param>
        /// <returns>Registered options</returns>
        public static JObject GetOptions(string section)
        {
            return _instance.GetOptions(section);
        }           
    }
}
