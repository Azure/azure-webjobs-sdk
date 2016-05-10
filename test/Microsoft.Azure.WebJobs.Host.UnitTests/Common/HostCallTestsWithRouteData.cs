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

        // Invoke with binding data only, no parameters. 
        [Fact]
        public async Task InvokeWithBindingData()
        {           
            string result = await Invoke(new { k1 = 100, k2 = 200 });
            Assert.Equal("100-x;200-y;100", result);
        }

        // Providing a direct parameter takes precedence overbinding data
        [Fact]
        public async Task Parameter_Takes_Precedence()
        {
            string result = await Invoke(new { k1 = 100, k2 = 200, p1="override" });
            Assert.Equal("override;200-y;100", result);
        }

        // Get an error when missing values. 
        [Fact]
        public async Task Missing()
        {
            try
            {
                string result = await Invoke(new { k1 = 100 });
            }
            catch (FunctionInvocationException e)
            {
                // There error should specifically be with p2. p1 and k1 binds ok since we supplied k1. 
                var msg1 = "Exception binding parameter 'p2'";
                Assert.Equal(msg1, e.InnerException.Message);

                var msg2 = "No value for named parameter 'k2'.";
                Assert.Equal(msg2, e.InnerException.InnerException.Message);
            }            
        }

        // Helper to invoke the method with the given parameters
        private async Task<string> Invoke(object arguments)
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
            await host.CallAsync(method, arguments);

            var x = testInstance._sb.ToString();

            return x;
        }
    }
}
