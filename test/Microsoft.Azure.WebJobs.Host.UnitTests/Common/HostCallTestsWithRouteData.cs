using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    // Unit test for exercising Host.Call passing route data. 
    public class HostCallTestsWithRouteData
    {
        public class Functions
        {
            public StringBuilder _sb = new StringBuilder();

            public void Func(
                [Test(Path = "{k1}-x")] string p1,
                [Test(Path = "{k2}-y")] string p2, 
                int k1)
            {
                _sb.AppendFormat("{0};{1};{2}", p1, p2, k1);
            }      
        }

        public class TestAttribute : Attribute
        {
            [AutoResolve]
            public string Path { get; set; }
        }

        public class FakeExtClient : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                IExtensionRegistry extensions = context.Config.GetService<IExtensionRegistry>();
                var bf = context.Config.BindingFactory;
                var rule = bf.BindToExactType<TestAttribute, string>(attr => attr.Path);
                extensions.RegisterBindingRules<TestAttribute>(rule);
            }
        }

        [Fact]
        public async Task Test()
        {
            var activator = new FakeActivator();
            JobHostConfiguration config = new JobHostConfiguration()
            {
                TypeLocator = new FakeTypeLocator(typeof(Functions)),
                JobActivator = activator
            };
            Functions testInstance = new Functions();
            activator.Add(testInstance);

            IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();
            FakeExtClient client = new FakeExtClient();
            extensions.RegisterExtension<IExtensionConfigProvider>(client);

            JobHost host = new JobHost(config);

            var method = typeof(Functions).GetMethod("Func");
            await host.CallAsync(method, new { k1 = 100, k2 = 200 });

            var x = testInstance._sb.ToString();

            Assert.Equal("100-x;200-y;100", x);
        }
    }
}
