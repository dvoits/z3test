param(
 [Parameter(Mandatory=$True)]
 [string]
 $storageName,

 [Parameter(Mandatory=$True)]
 [Microsoft.Azure.Commands.ResourceManager.Cmdlets.SdkModels.PSResourceGroup]
 $resourceGroup
)

$ErrorActionPreference = "Stop"

#Create or check for existing
$storage = Get-AzureRmStorageAccount -Name $storageName -ResourceGroupName $rg.ResourceGroupName -ErrorAction SilentlyContinue
if(!$storage)
{
    $storage = New-AzureRmStorageAccount -Name $storageName -ResourceGroupName $rg.ResourceGroupName -Location $rg.Location -SkuName Standard_LRS -Kind Storage
}

$storage