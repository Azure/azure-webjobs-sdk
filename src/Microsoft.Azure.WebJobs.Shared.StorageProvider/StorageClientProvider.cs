// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Shared.StorageProvider
{
    /// <summary>
    /// Abstraction to provide storage accounts from the connection names.
    /// This gets the storage account name via the binding attribute's <see cref="IConnectionProvider.Connection"/>
    /// property.
    /// If the connection is not specified on the attribute, it uses a default account.
    /// </summary>
    public abstract class StorageClientProvider<TClient, TClientOptions> where TClientOptions : ClientOptions
    {
        private readonly IConfiguration _configuration;
        private readonly AzureComponentFactory _componentFactory;
        private readonly AzureEventSourceLogForwarder _logForwarder;
        private readonly ILogger _logger;

        /// <summary>
        /// Generic StorageClientProvider
        /// </summary>
        /// <param name="configuration">Registered <see cref="IConfiguration"/></param>
        /// <param name="componentFactory">Registered <see cref="AzureComponentFactory"/></param>
        /// <param name="logForwarder">Registered <see cref="AzureEventSourceLogForwarder"/></param>
        public StorageClientProvider(IConfiguration configuration, AzureComponentFactory componentFactory, AzureEventSourceLogForwarder logForwarder, ILogger<TClient> logger)
        {
            _configuration = configuration;
            _componentFactory = componentFactory;
            _logForwarder = logForwarder;
            _logger = logger;
        }

        /// <summary>
        /// Attempts to get a storage client
        /// </summary>
        /// <param name="name">Name of the connection to use</param>
        /// <param name="client">client to be instantiated</param>
        /// <returns>indicates a successful client creation</returns>
        public virtual bool TryGet(string name, out TClient client)
        {
            try
            {
                client = Get(name);
                return true;
            }
            catch (Exception ex)
            {
                client = default(TClient);
                _logger.LogError(ex, "Could not create Storage Client");
                return false;
            }
        }

        /// <summary>
        /// Attempts to get a storage client
        /// </summary>
        /// <param name="name">Name of the connection string to use</param>
        /// <param name="client">client to be instantiated</param>
        /// <returns>indicates a successful client creation</returns>
        public virtual bool TryGetFromConnectionString(string connectionString, out TClient client)
        {
            try
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("connectionString", connectionString)
                }).Build();

                // AzureComponentFactory assumes IConfigurationSection with a value is a connection string
                var configSection = configuration.GetSection("connectionString");
                client = CreateClient(configSection, null, null);
                return true;
            }
            catch (Exception ex)
            {

                client = default(TClient);
                _logger.LogError(ex, "Could not create Storage Client from a connection string.");
                return false;
            }
        }

        /// <summary>
        /// Gets a storage client
        /// </summary>
        /// <param name="name">Connection name to resolve</param>
        /// <param name="resolver"><see cref="INameResolver"/> to use</param>
        /// <returns>storage client provider</returns>
        public virtual TClient Get(string name, INameResolver resolver)
        {
            var resolvedName = resolver.ResolveWholeString(name);
            return this.Get(resolvedName);
        }

        /// <summary>
        /// Gets a storage client
        /// </summary>
        /// <param name="name">Name of the connection to use</param>
        /// <returns>storage client provider</returns>
        public virtual TClient Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                name = ConnectionStringNames.Storage; // default
            }

            // $$$ Where does validation happen?
            IConfigurationSection connectionSection = _configuration.GetWebJobsConnectionStringSection(name);
            if (!connectionSection.Exists())
            {
                // Not found
                throw new InvalidOperationException($"Storage account connection string '{IConfigurationExtensions.GetPrefixedConnectionStringName(name)}' does not exist. Make sure that it is a defined App Setting.");
            }

            _logForwarder.Start();
            var credential = _componentFactory.CreateTokenCredential(connectionSection);
            var options = CreateClientOptions(connectionSection);
            return CreateClient(connectionSection, credential, options);
        }

        /// <summary>
        /// Creates a storage client
        /// </summary>
        /// <param name="configuration">Registered <see cref="IConfiguration"/></param>
        /// <param name="tokenCredential"><see cref="TokenCredential"/> to use for the client </param>
        /// <param name="options">Generic options to use for the client</param>
        /// <returns></returns>
        protected virtual TClient CreateClient(IConfiguration configuration, TokenCredential tokenCredential, TClientOptions options)
        {
            // If connection string is present, it will be honored first
            if (!IsConnectionStringPresent(configuration) && TryGetServiceUri(configuration, out Uri serviceUri))
            {
                var constructor = typeof(TClient).GetConstructor(new Type[] { typeof(Uri), typeof(TokenCredential), typeof(TClientOptions) });
                return (TClient)constructor.Invoke(new object[] { serviceUri, tokenCredential, options });
            }

            return (TClient)_componentFactory.CreateClient(typeof(TClient), configuration, tokenCredential, options);
        }

        /// <summary>
        /// The host account is for internal storage mechanisms like load balancer queuing.
        /// </summary>
        /// <returns></returns>
        public virtual TClient GetHost()
        {
            return this.Get(null);
        }

        /// <summary>
        /// Creates client options from the given configuration
        /// </summary>
        /// <param name="configuration">Registered <see cref="IConfiguration"/></param>
        /// <returns>Client options</returns>
        protected virtual TClientOptions CreateClientOptions(IConfiguration configuration)
        {
            var clientOptions = (TClientOptions)_componentFactory.CreateClientOptions(typeof(TClientOptions), null, configuration);
            clientOptions.Diagnostics.ApplicationId = clientOptions.Diagnostics.ApplicationId ?? "AzureWebJobs";
            if (SkuUtility.IsDynamicSku)
            {
                clientOptions.Transport = CreateTransportForDynamicSku();
            }

            return clientOptions;
        }

        /// <summary>
        /// Either constructs the serviceUri from the provided accountName
        /// or retrieves the serviceUri for the specific resource (i.e. blobServiceUri or queueServiceUri)
        /// </summary>
        /// <param name="configuration">Registered <see cref="IConfiguration"/></param>
        /// <param name="serviceUri">instantiates the serviceUri</param>
        /// <returns>retrieval success</returns>
        protected virtual bool TryGetServiceUri(IConfiguration configuration, out Uri serviceUri)
        {
            try
            {
                var serviceUriConfig = string.Format(CultureInfo.InvariantCulture, "{0}ServiceUri", ServiceUriSubDomain);

                if (configuration.GetValue<string>("accountName") != null)
                {
                    var accountName = configuration.GetValue<string>("accountName");
                    serviceUri = FormatServiceUri(accountName);
                    return true;
                }
                else if (configuration.GetValue<string>(serviceUriConfig) != null)
                {
                    var uri = configuration.GetValue<string>(serviceUriConfig);
                    serviceUri = new Uri(uri);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not parse serviceUri from the configuration.");
            }

            serviceUri = default(Uri);
            return false;
        }

        /// <summary>
        /// Todo: Eventually move this into storage sdk
        /// Generates the serviceUri for a particular storage resource
        /// </summary>
        /// <param name="accountName">accountName for the storage account</param>
        /// <param name="defaultProtocol">protocol to use for REST requests</param>
        /// <param name="endpointSuffix">endpoint suffix for the storage account</param>
        /// <returns></returns>
        protected virtual Uri FormatServiceUri(string accountName, string defaultProtocol = "https", string endpointSuffix = "core.windows.net")
        {
            var uri = string.Format(CultureInfo.InvariantCulture, "{0}://{1}.{2}.{3}", defaultProtocol, accountName, ServiceUriSubDomain, endpointSuffix);
            return new Uri(uri);
        }

        /// <summary>
        /// The subdomain for the resource (i.e. blob, queue, file)
        /// </summary>
#pragma warning disable CA1056 // URI-like properties should not be strings
        protected abstract string ServiceUriSubDomain { get; }
#pragma warning restore CA1056 // URI-like properties should not be strings

        private static HttpPipelineTransport CreateTransportForDynamicSku()
        {
            return new HttpClientTransport(new HttpClient(new HttpClientHandler()
            {
                MaxConnectionsPerServer = 50
            }));
        }

        private bool IsConnectionStringPresent(IConfiguration configuration)
        {
            return configuration is IConfigurationSection section && section.Value != null;
        }
    }
}
