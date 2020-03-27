# Remove-Item F:\LocalNugetRepository\microsoft.azure.webjobs.extensions.storage\ -Recurse -Confirm:$false -Force
# Remove-Item C:\Users\gochaudh\.nuget\packages\microsoft.azure.webjobs.extensions.storage\4.0.0-gochaudh\ -Recurse -Confirm:$false -Force
# Remove-Item C:\Users\gochaudh\.nuget\packages\microsoft.azure.webjobs.extensions.storage\4.0.0-gochaudh\ -Recurse -Confirm:$false -Force
# .\nuget.exe add F:\serverless_project\azure-webjobs-sdk\src\Microsoft.Azure.WebJobs.Extensions.Storage\bin\Debug\Microsoft.Azure.WebJobs.Extensions.Storage.4.0.0-gochaudh.nupkg -source F:\LocalNugetRepository\
Remove-Item F:\LocalNugetRepository\microsoft.azure.webjobs\ -Recurse -Confirm:$false -Force
Remove-Item C:\Users\gochaudh\.nuget\packages\microsoft.azure.webjobs\3.0.17 -Recurse -Confirm:$false -Force
Remove-Item C:\Users\gochaudh\.nuget\packages\microsoft.azure.webjobs\3.0.17 -Recurse -Confirm:$false -Force
.\nuget.exe add F:\serverless_project\azure-webjobs-sdk\src\Microsoft.Azure.WebJobs.Host\bin\Debug\Microsoft.Azure.WebJobs.3.0.17.nupkg -source F:\LocalNugetRepository\