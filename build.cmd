@ECHO OFF
PowerShell -NoProfile -NoLogo -ExecutionPolicy unrestricted -Command "./dotnet-install.ps1 -Version 3.1.416 -Architecture x86 %*; exit $LASTEXITCODE"
PowerShell -NoProfile -NoLogo -ExecutionPolicy unrestricted -Command "./build.ps1 %*; exit $LASTEXITCODE"