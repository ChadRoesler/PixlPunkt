# PixlPunkt Installation Guide

## Quick Installation

1. Download the latest release for your platform (x64 or ARM64)
2. Extract the ZIP file
3. **Right-click `Install-PixlPunkt.ps1` ? "Run with PowerShell"**
4. If prompted, allow running as Administrator
5. Launch PixlPunkt from the Start Menu

## Manual Installation

### Step 1: Install the Certificate

The signing certificate must be installed before the app can be installed. This is a one-time setup per machine.

1. Right-click on `PixlPunkt.cer`
2. Select **"Install Certificate"**
3. Choose **"Local Machine"** ? Click "Next"
4. Select **"Place all certificates in the following store"**
5. Click **"Browse"** and select **"Trusted People"**
6. Click "Next" ? "Finish"

### Step 2: Install the App

1. Double-click the `.msix` file
2. Click **"Install"** in the App Installer window
3. Wait for installation to complete

## Uninstallation

1. Open **Windows Settings**
2. Go to **Apps ? Installed apps**
3. Find "PixlPunkt" and click **"Uninstall"**

## Troubleshooting

### "App installation failed" Error

- **Cause**: Certificate not installed or installed in wrong location
- **Solution**: Follow the certificate installation steps above, making sure to:
  - Install to **Local Machine** (not Current User)
  - Place in **Trusted People** store

### "Windows protected your PC" SmartScreen Warning

This appears because the app is signed with a self-signed certificate rather than a commercial code signing certificate.

1. Click **"More info"**
2. Click **"Run anyway"**

### PowerShell Script Won't Run

If you get an execution policy error:

```powershell
# Run this once as Administrator:
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Then try running the install script again.

### App Won't Start After Installation

- Make sure you're running Windows 10 version 1809 or later
- Try restarting your computer
- Check Windows Event Viewer for error details

## System Requirements

| Requirement | Minimum |
|-------------|---------|
| OS | Windows 10 version 1809 (build 17763) |
| Architecture | x64 or ARM64 |
| RAM | 4 GB |
| Disk Space | 200 MB |

## For Developers

### Generating a New Signing Certificate

If you're forking this project and need to generate your own certificate:

```powershell
# Run from the repository root
.\scripts\Generate-SigningCertificate.ps1
```

This will create:
- `PixlPunkt.pfx` - Private key for signing (DO NOT COMMIT)
- `PixlPunkt.cer` - Public certificate for users
- `github-secrets.txt` - Values to add to GitHub Secrets

### GitHub Secrets Required

| Secret Name | Description |
|-------------|-------------|
| `SIGNING_CERTIFICATE_BASE64` | Base64-encoded PFX file |
| `SIGNING_CERTIFICATE_PASSWORD` | Password for the PFX file |

Add these in: Repository Settings ? Secrets and variables ? Actions

## Security Notice

This application is signed with a self-signed certificate for sideloading. The certificate is included in the download package. By installing this certificate, you're trusting applications signed by this certificate.

The PixlPunkt signing certificate is only used for PixlPunkt releases from the official repository.
