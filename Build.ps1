param (
  [string]$packageSuffix = "0"
)

dotnet --version

dotnet build Webjobs.sln -v q

if (-not $?) { exit 1 }

$projects = 
  "src\Microsoft.Azure.WebJobs\WebJobs.csproj",
  "src\Microsoft.Azure.WebJobs.Host\WebJobs.Host.csproj",
  "src\Microsoft.Azure.WebJobs.Logging\WebJobs.Logging.csproj",
  "src\Microsoft.Azure.WebJobs.Logging.ApplicationInsights\WebJobs.Logging.ApplicationInsights.csproj",
  "src\Microsoft.Azure.WebJobs.Extensions.EventHubs\WebJobs.EventHubs.csproj",
  "src\Microsoft.Azure.WebJobs.ServiceBus\WebJobs.ServiceBus.csproj",
  "src\Microsoft.Azure.WebJobs.Extensions.Storage\WebJobs.Extensions.Storage.csproj",
  "src\Microsoft.Azure.WebJobs.Host.Storage\WebJobs.Host.Storage.csproj"

foreach ($project in $projects)
{
  $cmd = "pack", "$project", "-o", "..\..\buildoutput", "--no-build"
  
  if ($packageSuffix -ne "0")
  {
    $cmd += "--version-suffix", "-$packageSuffix"
  }
  
  & dotnet $cmd  
}