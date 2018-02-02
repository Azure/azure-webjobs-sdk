param (
  [string]$buildNumber = "0"
)

dotnet --version

dotnet build Webjobs.sln -v q

if (-not $?) { exit 1 }

dotnet pack src\Microsoft.Azure.WebJobs\WebJobs.csproj -o ..\..\buildoutput --no-build --version-suffix $buildNumber

dotnet pack src\Microsoft.Azure.WebJobs.Host\WebJobs.Host.csproj -o ..\..\buildoutput --no-build --version-suffix $buildNumber

dotnet pack src\Microsoft.Azure.WebJobs.Logging\WebJobs.Logging.csproj -o ..\..\buildoutput --no-build --version-suffix $buildNumber

dotnet pack src\Microsoft.Azure.WebJobs.Logging.ApplicationInsights\WebJobs.Logging.ApplicationInsights.csproj -o ..\..\buildoutput --no-build --version-suffix $buildNumber

dotnet pack src\Microsoft.Azure.WebJobs.ServiceBus\EventHubs\WebJobs.EventHubs.csproj -o ..\..\..\buildoutput --no-build --version-suffix $buildNumber

dotnet pack src\Microsoft.Azure.WebJobs.ServiceBus\WebJobs.ServiceBus.csproj -o ..\..\buildoutput --no-build --version-suffix $buildNumber