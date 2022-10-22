// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Host.Scale
{
    /// <summary>
    /// The context object that is provided from Scale Controller to extensions
    /// It contains All the information that requires for instanciate the IScaleMonitor implementation
    /// The objectives are, provide the same configuration inteface for extensions and ScaleController
    /// </summary>
    public class ScaleMonitorContext
    {
        public ILoggerFactory LoggerFactory { get; set; }

        /// <summary>
        /// Contains Trigger information that is provided by SyncTrigger
        /// </summary>
        public string TriggerData { get; set; }

        public string FunctionId { get; set; }

        // TODO We need to come up with the idea how to make the Managed Identity as generic.
        // public List<ManagedIdentityInformation> ManagedIdentities { get; set; }

        /// <summary>
        /// Contains AppSettings, and hydrated host.json information
        /// </summary>
        public IConfiguration Configration { get; set; }

        public INameResolver NameResolver
        {
            get
            {
                return new DefaultNameResolver(Configration);
            }
        }

        /// <summary>
        /// Returns TriggerAttribute with hydrating the configuration. 
        /// </summary>
        /// <typeparam name="T">TriggerAttirbute type name (e.g. ServiceBusTriggerAttribute)</typeparam>
        /// <returns>T</returns>
        public T GetTriggerAttribute<T>()
        {
            T trigggerAttribute = JsonConvert.DeserializeObject<T>(TriggerData);

            return trigggerAttribute;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="extensionName"></param>
        /// <returns></returns>
        public T GetExtensionOption<T>(string extensionName)
        {
            var section = Configration.GetWebJobsExtensionConfigurationSection(extensionName);
            var instance = Activator.CreateInstance<T>();
            section.Bind(instance);
            return instance;
        }
    }
}
