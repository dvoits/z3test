param(
 [Parameter(Mandatory=$True)]
 [string]
 $name,

 [string]
 $password,

 [DateTime]
 $startDate,

 [DateTime]
 $endDate
)

$ErrorActionPreference = "Stop"

if (-not $startDate) {
    $startDate = Get-Date
}
if (-not $endDate) {
    $endDate = $startDate.AddYears(1)
}
$cert = New-SelfSignedCertificate -Subject ("CN=" + $name) -CertStoreLocation Cert:\CurrentUser\My -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider" -NotBefore $startDate -NotAfter $endDate -Type Custom -KeyExportPolicy ExportableEncrypted

if ($password) {
    $pwd = ConvertTo-SecureString -String $password -Force -AsPlainText
    $null = Export-PfxCertificate -cert $cert -FilePath (".\" + $name + ".pfx") -Password $pwd
}
#Remove-Item -Path ("Cert:\CurrentUser\My\" + $cert.Thumbprint) -DeleteKey
$cert