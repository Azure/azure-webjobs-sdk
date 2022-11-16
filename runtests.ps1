$success = $true

$projects = 
  "test\Microsoft.Azure.WebJobs.Host.UnitTests",
  "test\Microsoft.Azure.WebJobs.Host.FunctionalTests",
  "test\Microsoft.Azure.WebJobs.Logging.FunctionalTests",
  "test\Microsoft.Azure.WebJobs.Host.EndToEndTests"
  

foreach ($project in $projects)
{
  $cmd = "test", "$project", "-v", "m", "--no-build", "--logger", "trx;LogFileName=TEST.xml"

  if ($null -ne $env:Configuration)
  {
    $cmd += "--configuration", "$env:Configuration"
  }

  & dotnet $cmd  

  $success = $success -and $?
}


if (-not $success) { exit 1 }