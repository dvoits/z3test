param(
 [Parameter(Mandatory=$True)]
 [string]
 $resourceGroupName,

 [string]
 $resourceGroupLocation
)

$ErrorActionPreference = "Stop"

#Create or check for existing resource group
$resourceGroup = Get-AzureRmResourceGroup -Name $resourceGroupName -ErrorAction SilentlyContinue
if(!$resourceGroup)
{
    if(!$resourceGroupLocation) {
        Write-Host "Resource group '$resourceGroupName' does not exist. To create a new resource group, please enter a location.";
        $resourceGroupLocation = Read-Host "resourceGroupLocation";
    }
    $resourceGroup = New-AzureRmResourceGroup -Name $resourceGroupName -Location $resourceGroupLocation
}

$resourceGroup