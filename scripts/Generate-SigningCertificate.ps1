#################################################################################
# Generate Self-Signed Certificate for MSIX Signing
#################################################################################
# This script creates a self-signed certificate for signing MSIX packages.
# The certificate is used for sideloading - users must install the .cer file
# as a Trusted Root Certificate Authority before installing the MSIX.
#################################################################################

param(
    [string]$Subject = "CN=PixlPunkt, O=PixlPunkt, C=US",
    [string]$FriendlyName = "PixlPunkt Code Signing Certificate",
    [string]$OutputPath = $(Get-Location).Path,
    [string]$PfxPassword = "PixlPunkt2024!",
    [int]$ValidYears = 5
)

$ErrorActionPreference = "Stop"

Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "  Generating Self-Signed Certificate for PixlPunkt MSIX Signing" -ForegroundColor Cyan
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host ""

# Ensure output directory exists
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

$pfxPath = Join-Path $OutputPath "PixlPunkt.pfx"
$cerPath = Join-Path $OutputPath "PixlPunkt.cer"

# Calculate expiration date
$notAfter = (Get-Date).AddYears($ValidYears)

Write-Host "[+] Creating certificate..." -ForegroundColor Yellow
Write-Host "    Subject: $Subject"
Write-Host "    Valid until: $notAfter"

# Create the certificate
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -FriendlyName $FriendlyName `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
    -NotAfter $notAfter `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -KeyLength 2048 `
    -KeyAlgorithm RSA `
    -HashAlgorithm SHA256

Write-Host "[+] Certificate created with thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

# Export to PFX (for signing)
Write-Host ""
Write-Host "   => Exporting PFX (for CI signing)..." -ForegroundColor Yellow

$securePassword = ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null

Write-Host "[+] PFX exported to: $pfxPath" -ForegroundColor Green

# Export to CER (for users to install)
Write-Host ""
Write-Host "[+] Exporting CER (for user installation)..." -ForegroundColor Yellow

Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

Write-Host "   => CER exported to: $cerPath" -ForegroundColor Green

# Get base64 of PFX for GitHub Secrets
Write-Host ""
Write-Host "[+] Generating Base64 for GitHub Secrets..." -ForegroundColor Yellow

$pfxBytes = [System.IO.File]::ReadAllBytes($pfxPath)
$pfxBase64 = [System.Convert]::ToBase64String($pfxBytes)

$secretsPath = Join-Path $OutputPath "github-secrets.txt"
@"
===================================================================
GitHub Secrets Configuration
===================================================================

Add these secrets to your GitHub repository:
  Settings > Secrets and variables > Actions > New repository secret

===================================================================
Secret Name: SIGNING_CERTIFICATE_BASE64
Secret Value (copy everything below this line until the next separator):
===================================================================
$pfxBase64
===================================================================

===================================================================
Secret Name: SIGNING_CERTIFICATE_PASSWORD
Secret Value:
===================================================================
$PfxPassword
===================================================================

Certificate Thumbprint (for reference): $($cert.Thumbprint)
===================================================================
"@ | Set-Content $secretsPath

Write-Host "? GitHub secrets saved to: $secretsPath" -ForegroundColor Green

# Clean up certificate from local store (optional)
Write-Host ""
Write-Host "[+] Cleaning up local certificate store..." -ForegroundColor Yellow
Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force

Write-Host ""
Write-Host "===================================================================" -ForegroundColor Green
Write-Host "  Certificate Generation Complete!" -ForegroundColor Green
Write-Host "===================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Add the secrets from 'github-secrets.txt' to your GitHub repository"
Write-Host "  2. Commit the .cer file to your repository (it's public/safe)"
Write-Host "  3. DO NOT commit the .pfx file or github-secrets.txt!"
Write-Host "  4. Users will need to install PixlPunkt.cer before installing the MSIX"
Write-Host ""
