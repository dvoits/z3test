param(
 [Parameter(Mandatory=$True)]
 [string]
 $appName,

 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.ResourceManager.Cmdlets.SdkModels.PSResourceGroup]
 $resourceGroup,
 
 [Parameter(Mandatory=$True)]
 [string]
 $connectionStringSecretName,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.Management.Storage.Models.PSStorageAccount]
 $storage,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.KeyVault.Models.PSVault]
 $keyVault,
 
 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.Resources.Models.ActiveDirectory.PSADServicePrincipal]
 $AADAppServicePrincipal,
 
 [Parameter(Mandatory=$True)]
 [System.Security.Cryptography.X509Certificates.X509Certificate2]
 $cert,
 
 [Parameter(Mandatory=$True)]
 [string]
 $certPassword
)

$ErrorActionPreference = "Stop"

$cpath = Get-Location
$cdir = $cpath.Path

if ([Environment]::Is64BitOperatingSystem) {
    $pfiles = ${env:PROGRAMFILES(X86)}
} else {
    $pfiles = $env:PROGRAMFILES
}
$publishModule = $pfiles + '\Microsoft Visual Studio 14.0\Common7\IDE\Extensions\Microsoft\Web Tools\Publish\Scripts\1.2.0\publish-module.psm1'
if (!(Test-Path $publishModule)) {
    Write-Error -Message 'ERROR: Failed to locate powershell publish-module for ASP.NET at ' + $publishModule
    exit 1
}
Import-Module $publishModule

#Create or check for existing
Write-Host "Retrieving WebApp..."
$app = Get-AzureRmWebApp -Name $appName -ResourceGroupName $resourceGroup.ResourceGroupName -ErrorAction SilentlyContinue
if(!$app)
{
    Write-Host "Not found, creating a new one..."
    $plan = Get-AzureRmAppServicePlan -Name $appName -ResourceGroupName $resourceGroup.ResourceGroupName -ErrorAction SilentlyContinue
    if (!$plan) {
        $plan = New-AzureRmAppServicePlan -Name $appName -ResourceGroupName $resourceGroup.ResourceGroupName -Location $resourceGroup.Location -Tier Basic
    }
    $app = New-AzureRmWebApp -Name $appName -ResourceGroupName $resourceGroup.ResourceGroupName -Location $resourceGroup.Location -AppServicePlan $plan.Name
}
$newSettings = @{}
ForEach ($kvp in $app.SiteConfig.AppSettings) {
    $newSettings[$kvp.Name] = $kvp.Value
}

$newSettings["WEBSITE_LOAD_CERTIFICATES"] = "*"

$null = Set-AzureRMWebApp -Name $appName -ResourceGroupName $resourceGroup.ResourceGroupName -AppSettings $newSettings

Write-Host "Uploading certificate..."
#Dirty hack, because Azure PowerDhell lacks required capablities
#$pfx = New-AzureRmApplicationGatewaySslCertificate -Name $appName -CertificateFile E:\PS\example.pfx -Password $certPassword
$pfxBlob = [System.Convert]::ToBase64String($cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $certPassword))
$ResourceLocation = "West Europe"
$ResourceName = "Newcertificate"
$PropertiesObject = @{
    pfxBlob = $pfxBlob
    password = $certPassword
}

$null = New-AzureRmResource -Name $ResourceName -Location $resourceGroup.Location -PropertyObject $PropertiesObject -ResourceGroupName $resourceGroup.ResourceGroupName -ResourceType Microsoft.Web/certificates -ApiVersion 2015-08-01 -Force


Write-Host "Retrieving publishing credentials..."
$siteConf = Invoke-AzureRmResourceAction -ResourceGroupName $resourceGroup.ResourceGroupName -ResourceType Microsoft.Web/sites/config -ResourceName $appName/publishingcredentials -Action list -ApiVersion 2015-08-01 -Force

Write-Host "Building NightlyWebApp..."
$null = .\Build-NightlyWebApp.ps1

Write-Host "Configuring NightlyWebApp..."
$confPath = Join-Path $cdir "\NightlyWebApp\Web.config"
$conf = [xml] (Get-Content $confPath)
($conf.configuration.applicationSettings.'Nightly.Properties.Settings'.setting | where {$_.name -eq 'KeyVaultUrl'}).Value = $keyVault.VaultUri
($conf.configuration.applicationSettings.'Nightly.Properties.Settings'.setting | where {$_.name -eq 'AADApplicationId'}).Value = $AADAppServicePrincipal.ApplicationId.ToString()
($conf.configuration.applicationSettings.'Nightly.Properties.Settings'.setting | where {$_.name -eq 'AADApplicationCertThumbprint'}).Value = $cert.Thumbprint
($conf.configuration.applicationSettings.'Nightly.Properties.Settings'.setting | where {$_.name -eq 'ConnectionStringSecretId'}).Value = $connectionStringSecretName
$conf.Save($confPath)

Write-Host "Publishing NightlyWebApp..."
$publishProperties = @{'WebPublishMethod' = 'MSDeploy';
                        'MSDeployServiceUrl' = "$appName.scm.azurewebsites.net:443";
                        'DeployIisAppPath' = $appName;
                        'Username' = $siteConf.properties.publishingUserName 
                        'Password' = $siteConf.properties.publishingPassword}

$null = Publish-AspNet -packOutput (Join-Path $cdir "NightlyWebApp") -publishProperties $publishProperties

Write-Host "Deleting Temporary Files..."
Remove-Item (Join-Path $cdir "\NightlyWebApp") -Recurse

$app