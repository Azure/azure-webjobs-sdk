# Microsoft.Azure.WebJobs.Logging.ApplicationInsights

This package adds Application Insights based logging capabilities to the WebJobs SDK. For more information, please visit https://go.microsoft.com/fwlink/?linkid=2279708.

## Commonly used types

- `ApplicationInsightsLoggingBuilderExtensions`
- `ApplicationInsightsLoggerProvider`
- `ApplicationInsightsLoggerOptions`

## Example usage

The below example demonstrates configuration of ApplicationInsights logging on host startup via the `AddApplicationInsightsWebJobs` builder method.

``` CSharp
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureWebJobs(b =>
            {
                b.AddAzureStorageCoreServices();
                b.AddAzureStorageQueues();
            })
            .ConfigureLogging((context, b) =>
            {
                // If this key exists in any config, use it to enable App Insights
                string appInsightsKey = context.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];
                if (!string.IsNullOrEmpty(appInsightsKey))
                {
                    b.AddApplicationInsightsWebJobs(o => o.InstrumentationKey = appInsightsKey);
                }
            });

        using var host = builder.Build();
        await host.RunAsync();
    }
}
```