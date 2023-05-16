param (
  [string]$buildVersion,
  [bool]$isPr = $false,
  [string]$artifactDirectory,
  [bool]$forceArtifacts = $false,
  [bool]$skipAssemblySigning = $false
)

if ($null -eq $buildVersion) {
  throw "Parameter $buildVersion cannot be null or empty. Exiting script."
}

if (-not (Test-Path $artifactDirectory)) {
  throw "Artifact directory '$artifactDirectory' not found. Exiting script."
}

if (-not $isPr -or $forceArtifacts) {
  $toSignPath = Join-Path -Path $artifactDirectory -ChildPath "*"
  $toSignZipPath = Join-Path -Path $artifactDirectory -ChildPath "tosign.zip"

  Write-Host "Zipping files for signing matching path: $toSignPath"
  Compress-Archive -Force -Path $toSignPath -DestinationPath $toSignZipPath
  Write-Host "Signing payload created at:" $toSignZipPath

  if ($skipAssemblySigning) {
    "Assembly signing disabled. Skipping signing process."
    exit 0;
  }

  if ($null -eq $env:FILES_ACCOUNT_NAME -or $null -eq $env:FILES_ACCOUNT_KEY ) {
    "Assembly signing credentials not present. Skipping signing process."
    exit 0;
  }

  Write-Host "Uploading signing job to storage."
  # This will fail if the artifacts already exist.
  $ctx = New-AzureStorageContext -StorageAccountName $env:FILES_ACCOUNT_NAME -StorageAccountKey $env:FILES_ACCOUNT_KEY
  Set-AzureStorageBlobContent -File $toSignZipPath -Container "webjobs" -Blob "$buildVersion.zip" -Context $ctx

  $queue = Get-AzureStorageQueue -Name "signing-jobs" -Context $ctx

  $messageBody = "SignNupkgs;webjobs;$buildVersion.zip"
  # $message = New-Object -TypeName Microsoft.WindowsAzure.Storage.Queue.CloudQueueMessage -ArgumentList $messageBody
  $queue.CloudQueue.AddMessage($messageBody)
}