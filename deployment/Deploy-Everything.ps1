param(
 [Parameter(Mandatory=$True)]
 [string]
 $name,

 [Parameter(Mandatory=$True)]
 [string]
 $certPassword,

 [Parameter(Mandatory=$True)]
 [string]
 $connectionStringSecretName,

 [string]
 $certPfxPath,

 [string]
 $location,

 [string]
 $storageName,

 [string]
 $batchName,

 [string]
 $keyVaultName,

 [string]
 $webAppName,
 
 [string]
 $referenceJsonPath,
 
 [string]
 $referenceExecutablePath,
 
 [string]
 $referenceInputPath,

 [string]
 $poolNameForNightlyRuns,

 [string]
 $poolNameForRunner
 )

$ErrorActionPreference = "Stop"

if (-not $storageName) {
    $storageName = $name.ToLowerInvariant()
}
if (-not $batchName) {
    $batchName = $name.ToLowerInvariant()
}
if (-not $keyVaultName) {
    $keyVaultName = $name.ToLowerInvariant()
}
if (-not $webAppName) {
    $webAppName = $name.ToLowerInvariant()
}

if (-not $location) {
    $location = "West Europe"
}

 if ([Environment]::Is64BitOperatingSystem) {
    $pfiles = ${env:PROGRAMFILES(X86)}
    $platform = '/p:Platform="x64"'
} else {
    $pfiles = $env:PROGRAMFILES
    $platform = '/p:Platform="x86"'
}
$msbuild = $pfiles + '\MSBuild\14.0\Bin\MSBuild.exe'
if (!(Test-Path $msbuild)) {
    Write-Error -Message 'ERROR: Failed to locate MSBuild at ' + $msbuild
    exit 1
}


if($certPfxPath) {
    Write-Host "Importing certificate..."
    $cert = Import-PfxCertificate -FilePath $certPfxPath -CertStoreLocation Cert:\CurrentUser\My -Password (ConvertTo-SecureString -String $certPassword -Force -AsPlainText) -Exportable
} else {
    Write-Host "Creating certificate..."
    $now = Get-Date
    $yearFromNow = $now.AddYears(1)
    [System.Security.Cryptography.X509Certificates.X509Certificate2]$cert = .\New-Cert.ps1 $name $certPassword $now $yearFromNow
}
Write-Host "Registering AAD application..."
[Microsoft.Azure.Commands.Resources.Models.ActiveDirectory.PSADServicePrincipal]$sp = .\New-AADApp.ps1 $name $cert
Write-Host "Creating resource group, if needed..."
[Microsoft.Azure.Commands.ResourceManager.Cmdlets.SdkModels.PSResourceGroup]$rg = .\Deploy-ResourceGroup.ps1 $name $location
Write-Host "Deploying storage..."
[Microsoft.Azure.Commands.Management.Storage.Models.PSStorageAccount]$storage = .\Deploy-Storage.ps1 $storageName $rg
Write-Host "Deploying batch account..."
[Microsoft.Azure.Commands.Batch.BatchAccountContext]$batch = .\Deploy-Batch.ps1 $batchName $rg $storage $cert $certPassword
Write-Host "Deploying key vault..."
[Microsoft.Azure.Commands.KeyVault.Models.PSVault]$vault = .\Deploy-KeyVault.ps1 $keyVaultName $rg $connectionStringSecretName $storage $batch $sp
Write-Host "Deploying AzureWorker..."
.\Deploy-AzureWorker.ps1 $connectionStringSecretName $storage $vault $sp $cert.Thumbprint
Write-Host "Deploying NightlyWebApp..."
[Microsoft.Azure.Management.WebSites.Models.Site]$webApp = .\Deploy-WebApp.ps1 $webAppName $rg $connectionStringSecretName $storage $vault $sp $cert $certPassword
if ($referenceJsonPath -and $referenceExecutablePath) {
    Write-Host "Deploying reference experiment..."
    .\Deploy-ReferenceExperiment.ps1 $storage $referenceJsonPath $referenceExecutablePath $referenceInputPath
}
Write-Host "Deploying NightlyRunner..."
$res = .\Deploy-NightlyRunner.ps1 $rg $connectionStringSecretName $storage $batch $vault $sp $cert.Thumbprint $poolNameForNightlyRuns $poolNameForRunner

Remove-Item -Path ("Cert:\CurrentUser\My\" + $cert.Thumbprint) -DeleteKey

