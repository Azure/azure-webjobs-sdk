// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SampleHost
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = new HostBuilder()
                .UseEnvironment("Development")
                .ConfigureWebJobs(b =>
                {
                    b.AddAzureStorageCoreServices()
                    .AddAzureStorage();
                })
                .ConfigureAppConfiguration(b =>
                {
                    // Adding command line as a configuration source
                    b.AddCommandLine(args);
                })
                .ConfigureLogging((context, b) =>
                {
                    b.SetMinimumLevel(LogLevel.Debug);
                    b.AddConsole();

                    // If this key exists in any config, use it to enable App Insights
                    string appInsightsKey = context.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
                    if (!string.IsNullOrEmpty(appInsightsKey))
                    {
                        b.AddApplicationInsightsWebJobs(o => o.InstrumentationKey = appInsightsKey);
                    }
                })
                //.ConfigureServices((services) =>
                //{
                //    services.Configure<JobHostInternalStorageOptions>(options =>
                //    {
                //        options.TenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
                //        options.StorageAccountName = "mercz11";
                //        options.AccessToken = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6IkhsQzBSMTJza3hOWjFXUXdtak9GXzZ0X3RERSIsImtpZCI6IkhsQzBSMTJza3hOWjFXUXdtak9GXzZ0X3RERSJ9.eyJhdWQiOiJodHRwczovL21hbmFnZW1lbnQuY29yZS53aW5kb3dzLm5ldC8iLCJpc3MiOiJodHRwczovL3N0cy53aW5kb3dzLm5ldC83MmY5ODhiZi04NmYxLTQxYWYtOTFhYi0yZDdjZDAxMWRiNDcvIiwiaWF0IjoxNTgyNzMzMjE1LCJuYmYiOjE1ODI3MzMyMTUsImV4cCI6MTU4MjczNzExNSwiYWlvIjoiNDJOZ1lKZ2ZjV1dhdyt4VlRFcS9DMmRxTnA2OUNBQT0iLCJhcHBpZCI6ImI3NzFlOWYwLTQzODctNDI0ZC1iYmU5LTQ2ZjI1ZDY5M2IzYSIsImFwcGlkYWNyIjoiMSIsImlkcCI6Imh0dHBzOi8vc3RzLndpbmRvd3MubmV0LzcyZjk4OGJmLTg2ZjEtNDFhZi05MWFiLTJkN2NkMDExZGI0Ny8iLCJvaWQiOiI4N2Q1YTFmZi1mN2MzLTRjOTUtYjM0My00Y2M2MjZiOWM4NmUiLCJzdWIiOiI4N2Q1YTFmZi1mN2MzLTRjOTUtYjM0My00Y2M2MjZiOWM4NmUiLCJ0aWQiOiI3MmY5ODhiZi04NmYxLTQxYWYtOTFhYi0yZDdjZDAxMWRiNDciLCJ1dGkiOiJ1WEh0azYtLTJVeW9IYS1ONXlJaEFBIiwidmVyIjoiMS4wIn0.UkJWv2cvu_rL34ijbGXhG-aFDasRH53A_rxmK0F16I7m6lj9MwMfyOMjz2OciF63z1vlEMXngK4SdgvoeIW8AJBotcN5g1IgQF6b2RSHB4hwU01t3wrqgQZY_W2ovKFwT9mdrgFrf2eT7JJq3Il0LWkGeLULcKT9qtRokzEdOI1_pGdQ0XhwB6L_gOIeg9BPZyxBGWnXACWGy70kHJg1qR0tCx1mSDunJkBOiwoHdkffpDgl9_BZ2TpNK8rf8ti4yo-pREbvpJ99-qqaBXoibdTc1730GgTzlyNgoIFP0mVUrSbn8eFNptEi2_y73pDipB-J_CRQcvFU54BaxQoUIA";
                //    });
                //})
                .UseConsoleLifetime();

            var host = builder.Build();
            using (host)
            {
                await host.RunAsync();
            }
        }
    }
}
