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

if ((-not $isPr -and -not $skipAssemblySigning) -or $forceArtifacts) {
  $timeout = New-TimeSpan -Minutes 15
  Write-Host "Set polling timeout to:" $timeout.ToString()

  $sw = [System.Diagnostics.Stopwatch]::StartNew();
  $polling = $true;

  Write-Host "Connecting to storage account."
  $ctx = New-AzureStorageContext -StorageAccountName $env:FILES_ACCOUNT_NAME -StorageAccountKey $env:FILES_ACCOUNT_KEY
  $blob = $null;
  while ($sw.elapsed -lt $timeout -and $polling) {
    Write-Host "Retrieving Jenkins artifacts.."
    $blob = Get-AzureStorageBlob -Blob "$buildVersion.zip" -Container "webjobs-signed" -Context $ctx -ErrorAction Ignore
    if (-not $blob) {
      Write-Host "Jenkins artifacts not found. ${sw.Elapsed} elapsed. Polling..."
    }
    else {
      Write-Host "Jenkins artifacts found."
      $polling = $false;
    }
    Start-Sleep -Seconds 5
  }

  $sw.Stop();

  if ($polling) {
    Write-Host "No Jenkins artifacts found after ${sw.Elapsed}. Investigate job at https://funkins-master.redmond.corp.microsoft.com/job/Build_signing/"
    exit(1);
  }

  Write-Host "Removing directory $artifactDirectory"
  Remove-Item -Path $artifactDirectory -Recurse -Force

  Write-Host "Recreating directory $artifactDirectory"
  New-Item -ItemType "directory" -Path $artifactDirectory
  $signedZipPath = Join-Path -Path $artifactDirectory -ChildPath "signed.zip"

  Write-Host "Downloading signed file zip $buildVersion.zip to $signedZipPath"
  Get-AzureStorageBlobContent -Blob "$buildVersion.zip" -Container "webjobs-signed" -Destination $signedZipPath -Context $ctx
  
  Write-Host "Unzipping signed files to $artifactDirectory"
  Expand-Archive -LiteralPath $signedZipPath -DestinationPath $artifactDirectory

  Write-Host "Removing signed file zip ${Split-Path -Path $signedZipPath -Leaf}."
  Remove-Item -Path $signedZipPath

  if (-not $?) { exit 1 }
}