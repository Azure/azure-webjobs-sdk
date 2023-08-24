// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Description;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.DependencyInjection;
using static Microsoft.Azure.WebJobs.Extensions.Rpc.TestService;

namespace Microsoft.Azure.WebJobs.Extensions.Rpc
{
    public class WebJobsExtensionBuilderExtensionsTests
    {
        private IServiceCollection _services = new ServiceCollection();

        IWebJobsExtensionBuilder _extension;

        public WebJobsExtensionBuilderExtensionsTests()
        {
            _extension = Mock.Of<IWebJobsExtensionBuilder>(
                m => m.Services == _services && m.ExtensionInfo == ExtensionInfo.FromExtension<TestExtension>());
        }

        [Fact]
        public void MapGrpcService_Maps()
        {
            _extension.MapWorkerGrpcService<Service>();

            ServiceDescriptor descriptor = _services.FirstOrDefault(x => x.ServiceType == typeof(IRpcExtension));

            Assert.NotNull(descriptor);
            Assert.IsType<GrpcExtension<Service>>(descriptor.ImplementationInstance);
        }

        [Extension("Test", "test")]
        private class TestExtension : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                throw new NotImplementedException();
            }
        }

        private class Service : TestServiceBase
        {
        }
    }
}
