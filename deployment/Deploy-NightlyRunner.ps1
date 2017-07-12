param([Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.ResourceManager.Cmdlets.SdkModels.PSResourceGroup]
 $resourceGroup,
 
 [Parameter(Mandatory=$True)]
 [string]
 $connectionStringSecretName,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.Management.Storage.Models.PSStorageAccount]
 $storage,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.Batch.BatchAccountContext]
 $batchAccount,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.KeyVault.Models.PSVault]
 $keyVault,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.Resources.Models.ActiveDirectory.PSADServicePrincipal]
 $AADAppServicePrincipal,
 
 [Parameter(Mandatory=$True)]
 [string]
 $certThumbprint,

 [string]
 $poolNameForRuns,

 [string]
 $poolNameForRunner
)

$ErrorActionPreference = "Stop"

$cpath = Get-Location
$cdir = $cpath.Path

if (-not $poolNameForRuns -or -not $poolNameForRunner) {
    $pools = Get-AzureBatchPool -BatchContext $batchAccount
    if (-not $pools -or $pools.Length -eq 0) {
        Write-Error "Unable to deploy NightlyRunner: no pool name provided and no pools found in batch account."
        exit 1
    }
    if (-not $poolNameForRuns) {
        $poolNameForRuns = $pools[0].Id
    }
    if (-not $poolNameForRunner) {
        $poolNameForRunner = $pools[0].Id
    }
}

$batchAccount = Get-AzureRmBatchAccountKeys -AccountName $batchAccount.AccountName -ResourceGroupName $resourceGroup.ResourceGroupName

Write-Host "Building NightlyRunner..."
$null = .\Build-NightlyRunner.ps1
$null = mkdir "NightlyRunner" -Force
Copy-Item ..\src\NightlyRunner\bin\Release\*.exe .\NightlyRunner
Copy-Item ..\src\NightlyRunner\bin\Release\*.dll .\NightlyRunner
Copy-Item ..\src\NightlyRunner\bin\Release\*.config .\NightlyRunner

Write-Host "Configuring NightlyRunner..."
$confPath = Join-Path $cdir "\NightlyRunner\NightlyRunner.exe.config"
$conf = [xml] (Get-Content $confPath)
($conf.configuration.applicationSettings.'NightlyRunner.Properties.Settings'.setting | where {$_.name -eq 'KeyVaultUrl'}).Value = $keyVault.VaultUri
($conf.configuration.applicationSettings.'NightlyRunner.Properties.Settings'.setting | where {$_.name -eq 'AADApplicationId'}).Value = $AADAppServicePrincipal.ApplicationId.ToString()
($conf.configuration.applicationSettings.'NightlyRunner.Properties.Settings'.setting | where {$_.name -eq 'AADApplicationCertThumbprint'}).Value = $certThumbprint
($conf.configuration.applicationSettings.'NightlyRunner.Properties.Settings'.setting | where {$_.name -eq 'ConnectionStringSecretId'}).Value = $connectionStringSecretName
($conf.configuration.applicationSettings.'NightlyRunner.Properties.Settings'.setting | where {$_.name -eq 'AzureBatchPoolId'}).Value = $poolNameForRuns
$conf.Save($confPath)

Write-Host "Zipping NightlyRunner..."
$zip = Compress-Archive -Path ".\NightlyRunner\*" -DestinationPath ".\NightlyRunner.zip" -Force

Write-Host "Creating Application Package..."
$now = Get-Date
$version = $now.Year.ToString() + "-" + $now.Month.ToString() + "-" + $now.Day.ToString()
$zipPath = (Join-Path $cdir "\NightlyRunner.zip")
$null = New-AzureRmBatchApplicationPackage -AccountName $batchAccount.AccountName -ResourceGroupName $resourceGroup.ResourceGroupName -ApplicationId "NightlyRunner" -ApplicationVersion $version -FilePath $zipPath -Format "zip"
$null = Set-AzureRmBatchApplication -AccountName $batchAccount.AccountName -ResourceGroupName $resourceGroup.ResourceGroupName -ApplicationId "NightlyRunner" -DefaultVersion $version

Write-Host "Scheduling daily execution..."
$NightlyApp = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSApplicationPackageReference"
$NightlyApp.ApplicationId = "NightlyRunner" # <-- check application id
[Microsoft.Azure.Commands.Batch.Models.PSApplicationPackageReference[]] $AppRefs = @($NightlyApp)

$ManagerTask = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSJobManagerTask"
$ManagerTask.ApplicationPackageReferences = $AppRefs
$ManagerTask.Id = "NightlyRunTask"
$ManagerTask.CommandLine = "cmd /c %AZ_BATCH_APP_PACKAGE_NIGHTLYRUNNER%\NightlyRunner.exe"

$JobSpecification = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSJobSpecification"
$JobSpecification.JobManagerTask = $ManagerTask
$JobSpecification.PoolInformation = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSPoolInformation"
$JobSpecification.PoolInformation.PoolId = $poolNameForRunner

$Schedule = New-Object -TypeName "Microsoft.Azure.Commands.Batch.Models.PSSchedule"
$Schedule.RecurrenceInterval = [TimeSpan]::FromDays(1)

$null = New-AzureBatchJobSchedule -Id "NightlyRunSchedule" -Schedule $Schedule -JobSpecification $JobSpecification -BatchContext $batchAccount

Write-Host "Deleting Temporary Files"
Remove-Item (Join-Path $cdir "\NightlyRunner") -Recurse
Remove-Item $zipPath