# Microsoft.Azure.WebJobs.Host.Storage

This package contains Azure Storage based implementations of some WebJobs SDK component interfaces. For more information, please visit https://go.microsoft.com/fwlink/?linkid=2279708.

## Commonly used types

The main types exposed by this package are host builder extension methods to `IWebJobsBuilder`, provided by the following Types:

- `RuntimeStorageWebJobsBuilderExtensions`
- `StorageServiceCollectionExtensions`
- `StorageServiceCollectionExtensions`

## Example usage

The below example demonstrates the registration of Azure Storage services via the `AddAzureStorageCoreServices` builder method.

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