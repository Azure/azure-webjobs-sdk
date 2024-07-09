# Microsoft.Azure.WebJobs.Core

This package provides the core Types and Attribute definitions for the WebJobs SDK. For more information, please visit https://go.microsoft.com/fwlink/?linkid=2279708.

## Commonly used types

- `TimeoutAttribute`
- `SingletonAttribute`
- `FunctionNameAttribute`
- `ExtensionAttribute`

## Example usage

The below example shows a QueueTrigger function with a timeout defined.

``` CSharp
using Microsoft.Azure.WebJobs;

[TimeoutAttribute("00:15:00")]
public void ProcessWorkItem([QueueTrigger("test")] WorkItem workItem, ILogger logger)
{
    logger.LogInformation($"Processed work item {workItem.ID}");
}
```