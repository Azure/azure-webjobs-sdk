# Microsoft.Azure.WebJobs.Extensions.Rpc

This package provides RPC capabilities to the WebJobs SDK, allowing extensions to communicate between the host and worker via RPC. For more information, please visit https://go.microsoft.com/fwlink/?linkid=2279708.

## Commonly used types

- `WebJobsExtensionBuilderRpcExtensions`

## Example usage

The below example demonstrates how an extension can register a custom gRPC extension.

``` CSharp
public static IWebJobsBuilder AddMyExtension(this IWebJobsBuilder builder, Action<MyExtensionOptions> configure)
{
    builder.AddExtension<MyExtensionConfigProvider>()
        .MapWorkerGrpcService<MyGrpcService>();

    builder.Services.AddSingleton<MyGrpcService>();

    return builder;
}
```