$isPr = Test-Path env:APPVEYOR_PULL_REQUEST_NUMBER
$directoryPath = Split-Path $MyInvocation.MyCommand.Path -Parent

if (-not $isPr -or $env:ForceArtifacts -eq "1") {
  Write-Host "Zipping output for signing"

  Compress-Archive -Force $directoryPath\..\..\..\buildoutput\* $directoryPath\..\..\..\buildoutput\tosign.zip
  Write-Host "Signing payload created at " $directoryPath\..\..\..\buildoutput\tosign.zip

  if ($env:SkipAssemblySigning -eq "true") {
    "Assembly signing disabled. Skipping signing process."
    exit 0;
  }

  if ($env:FILES_ACCOUNT_NAME -eq $null -or $env:FILES_ACCOUNT_KEY -eq $null) {
    "Assembly signing credentials not present. Skipping signing process."
    exit 0;
  }

  Write-Host "Uploading signing job to storage"

  $ctx = New-AzureStorageContext $env:FILES_ACCOUNT_NAME $env:FILES_ACCOUNT_KEY
  Set-AzureStorageBlobContent "$directoryPath/../../../buildoutput/tosign.zip" "webjobs" -Blob "$env:APPVEYOR_BUILD_VERSION.zip" -Context $ctx

  $queue = Get-AzureStorageQueue "signing-jobs" -Context $ctx

  $messageBody = "SignNupkgs;webjobs;$env:APPVEYOR_BUILD_VERSION.zip"
  # $message = New-Object -TypeName Microsoft.WindowsAzure.Storage.Queue.CloudQueueMessage -ArgumentList $messageBody
  $queue.CloudQueue.AddMessage($messageBody)
}