param(
 [Parameter(Mandatory=$True)]
 [string]
 $keyVaultName,

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
 [Microsoft.Azure.Commands.Batch.BatchAccountContext]
 $batchAccount,

 [Microsoft.Azure.Commands.Resources.Models.ActiveDirectory.PSADServicePrincipal]
 $AADAppServicePrincipal
)

$ErrorActionPreference = "Stop"

#Create or check for existing
$keyVault = Get-AzureRmKeyVault -VaultName $keyVaultName -ResourceGroupName $resourceGroup.ResourceGroupName -ErrorAction SilentlyContinue
if(!$keyVault)
{
    $keyVault = New-AzureRmKeyVault -VaultName $keyVaultName -ResourceGroupName $resourceGroup.ResourceGroupName -Location $resourceGroup.Location
}

$storageKeys = Get-AzureRmStorageAccountKey -Name $storage.StorageAccountName -ResourceGroupName $resourceGroup.ResourceGroupName
$stName = $storage.StorageAccountName
$stKey = $storageKeys[0].Value
$batchAccount = Get-AzureRmBatchAccountKeys -AccountName $batchAccount.AccountName -ResourceGroupName $resourceGroup.ResourceGroupName
$batchName = $batchAccount.AccountName
$batchKey = $batchAccount.PrimaryAccountKey
$batchAddr = $batchAccount.AccountEndpoint

$connectionString = "DefaultEndpointsProtocol=https;AccountName=$stName;AccountKey=$stKey;BatchAccount=$batchName;BatchURL=https://$batchAddr;BatchAccessKey=$batchKey;"
$secureConnString = ConvertTo-SecureString -String $connectionString -Force -AsPlainText
$null = Set-AzureKeyVaultSecret -Name $connectionStringSecretName -SecretValue $secureConnString -VaultName $keyVaultName

if ($AADAppServicePrincipal) {
    Set-AzureRmKeyVaultAccessPolicy -VaultName $keyVaultName -ObjectId $AADAppServicePrincipal.Id -PermissionsToSecrets all -ResourceGroupName $resourceGroup.ResourceGroupName
}

$keyVault