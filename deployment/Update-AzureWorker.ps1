param(
 [Parameter(Mandatory=$True)]
 [string]
 $name,

 [string]
 $location,

 [string]
 $storageName
)

$ErrorActionPreference = "Stop"

if (-not $storageName) {
    $storageName = $name.ToLowerInvariant()
}

$cpath = Get-Location
$cdir = $cpath.Path

[Microsoft.Azure.Commands.ResourceManager.Cmdlets.SdkModels.PSResourceGroup]$rg = .\Deploy-ResourceGroup.ps1 $name $location
[Microsoft.Azure.Commands.Management.Storage.Models.PSStorageAccount]$storage = .\Deploy-Storage.ps1 $storageName $rg

Write-Host "Building AzureWorker..."
$null = .\Build-AzureWorker.ps1
$null = mkdir "AzureWorker" -Force
Copy-Item ..\src\AzurePerformanceTest\AzureWorker\bin\Release\*.exe .\AzureWorker
Copy-Item ..\src\AzurePerformanceTest\AzureWorker\bin\Release\*.dll .\AzureWorker

Write-Host "Retrieving configuration blob container..."
$container = Get-AzureStorageContainer -Name "config" -Context $storage.Context -ErrorAction SilentlyContinue
if (-not $container) {
    Write-Host "Container does not exist, creating new one..."
    $container = New-AzureStorageContainer -Name "config" -Permission Off -Context $storage.Context
}

Write-Host "Uploading AzureWorker..."
foreach($file in Get-ChildItem (Join-Path $cdir "\AzureWorker") -File)
{
    $null = Set-AzureStorageBlobContent -Blob $file.Name -CloudBlobContainer $container.CloudBlobContainer -File $file.FullName -Context $storage.Context -Force
}

Write-Host "Deleting Temporary Files"
Remove-Item (Join-Path $cdir "\AzureWorker") -Recurse