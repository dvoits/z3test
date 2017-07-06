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
$keyVault = Get-AzureRmKeyVault -VaultName $keyVaultName -ResourceGroupName $rg.ResourceGroupName -ErrorAction SilentlyContinue
if(!$keyVault)
{
    $keyVault = New-AzureRmKeyVault -VaultName $keyVaultName -ResourceGroupName $rg.ResourceGroupName -Location $rg.Location
}

$storageKeys = Get-AzureRmStorageAccountKey -Name $storage.StorageAccountName -ResourceGroupName $rg.ResourceGroupName
$stName = $storage.StorageAccountName
$stKey = $storageKeys[0].Value
$batchAccount = Get-AzureRmBatchAccountKeys -AccountName $batchAccount.AccountName -ResourceGroupName $rg.ResourceGroupName
$batchName = $batchAccount.AccountName
$batchKey = $batchAccount.PrimaryAccountKey
$batchAddr = $batchAccount.AccountEndpoint

$connectionString = "DefaultEndpointsProtocol=https;AccountName=$stName;AccountKey=$stKey;BatchAccount=$batchName;BatchURL=https://$batchAddr;BatchAccessKey=$batchKey;"
$secureConnString = ConvertTo-SecureString -String $connectionString -Force -AsPlainText
$null = Set-AzureKeyVaultSecret -Name $connectionStringSecretName -SecretValue $secureConnString -VaultName $keyVaultName

if ($AADAppServicePrincipal) {
    Set-AzureRmKeyVaultAccessPolicy -VaultName $keyVaultName -ObjectId $AADAppServicePrincipal.Id -PermissionsToSecrets all -ResourceGroupName $rg.ResourceGroupName
}

$keyVault