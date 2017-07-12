param(
 [Parameter(Mandatory=$True)]
 [string]
 $name,
 
 [Parameter(Mandatory=$True)]
 [System.Security.Cryptography.X509Certificates.X509Certificate2]
 $cert
)

$ErrorActionPreference = "Stop"

#Create or check for existing resource group
$apps = Get-AzureRmADApplication -DisplayNameStartWith $name -ErrorAction SilentlyContinue
if(!$apps)
{
    .\New-AADApp.ps1 $name $cert
} else {
    Write-Host "Found one or more AAD apps with that name. Which should be used?"
    Write-Host "0) Create a new one"
    for ($i = 1; $i -le $apps.Length; ++$i) {
        Write-Host "$i) $($apps[$i-1].DisplayName) ($($apps[$i-1].IdentifierUris[0]), $($apps[$i-1].ApplicationId))"
    }
    $num = -1
    $succ = [System.Int32]::TryParse($ans, [ref] $num)
    while (-not $succ -or $num -lt 0 -or $num -gt $apps.Length) {
        $ans = Read-Host -Prompt "Please, type a number between 0 and $($apps.Length)"
        $succ = [System.Int32]::TryParse($ans, [ref] $num)
    }
    if ($num -eq 0) {
        .\New-AADApp.ps1 $name $cert
    } else {
        $app = $apps[$num - 1]
        $credValue = [System.Convert]::ToBase64String($cert.GetRawCertData())
        $cred = New-AzureRmADAppCredential -ApplicationId $app.ApplicationId -CertValue $credValue -StartDate $cert.NotBefore -EndDate $cert.NotAfter
        $sp = Get-AzureRmADServicePrincipal -ServicePrincipalName $app.ApplicationId
        $sp
    }
}