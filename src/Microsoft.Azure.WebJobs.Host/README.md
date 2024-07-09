# Microsoft.Azure.WebJobs.Host

This package contains the runtime host components of the WebJobs SDK. For more information, please visit https://go.microsoft.com/fwlink/?linkid=2279708.

## Commonly used types

- `WebJobsHostBuilderExtensions`
- `JobHost`
- `IExtensionConfigProvider`

## Example usage

The below example demonstrates configuration and startup of a job host running in a console application.

``` CSharp
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureWebJobs(b =>
            {
                b.AddAzureStorageCoreServices();
                b.AddAzureStorageQueues();
            });

        using var host = builder.Build();
        await host.RunAsync();
    }
}
```