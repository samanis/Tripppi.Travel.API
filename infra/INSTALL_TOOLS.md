# AWS CLI and Terraform Installation Guide

## Option 1: Using Chocolatey (Easiest)

### Step 1: Open PowerShell as Administrator
- Press Windows Key
- Type "PowerShell"
- Right-click "Windows PowerShell"
- Select "Run as Administrator"

### Step 2: Run Installation Script
```powershell
cd C:\Users\saman\source\repos\Tripppi.Travel.API\infra
.\install-aws-terraform.ps1
```

### Step 3: Close and Reopen Terminal
Close PowerShell and open a **new regular PowerShell** (no admin needed).

### Step 4: Verify Installation
```powershell
aws --version
terraform version
```

---

## Option 2: Manual Download (Alternative)

If Chocolatey doesn't work:

### AWS CLI

1. **Download AWS CLI v2 for Windows**
   - URL: https://awscli.amazonaws.com/AWSCLIV2.msi
   - OR visit: https://aws.amazon.com/cli/

2. **Run the MSI installer**
   - Double-click the downloaded file
   - Follow installation wizard
   - Default settings are fine

3. **Verify installation**
   ```powershell
   aws --version
   # Expected: aws-cli/2.x.x Python/3.x.x Windows/x.x.x
   ```

### Terraform

1. **Download Terraform for Windows**
   - URL: https://www.terraform.io/downloads
   - Select "Windows" → "AMD64"
   - Downloads as: `terraform_X.X.X_windows_amd64.zip`

2. **Install Terraform**
   ```powershell
   # Create directory
   New-Item -ItemType Directory -Force -Path C:\terraform
   
   # Extract ZIP to C:\terraform
   # (Right-click ZIP → Extract All → C:\terraform)
   
   # Add to PATH
   $env:Path += ";C:\terraform"
   [Environment]::SetEnvironmentVariable("Path", $env:Path + ";C:\terraform", "Machine")
   ```

3. **Verify installation**
   ```powershell
   # Close and reopen PowerShell
   terraform version
   # Expected: Terraform vX.X.X
   ```

---

## Option 3: Using winget (Windows Package Manager)

If you have Windows 11 or Windows 10 with winget:

```powershell
# Install AWS CLI
winget install Amazon.AWSCLI

# Install Terraform
winget install Hashicorp.Terraform
```

---

## After Installation: Configure AWS CLI

### Step 1: Get AWS Credentials

You need:
- AWS Access Key ID
- AWS Secret Access Key
- Default region (e.g., `us-east-1`)

**To create credentials:**
1. Login to AWS Console: https://console.aws.amazon.com
2. Click your name (top-right) → Security Credentials
3. Scroll to "Access keys" → Create access key
4. Select "Command Line Interface (CLI)"
5. Download or copy the credentials

### Step 2: Configure AWS CLI

```powershell
aws configure
```

It will prompt you for:
```
AWS Access Key ID [None]: AKIAIOSFODNN7EXAMPLE
AWS Secret Access Key [None]: wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
Default region name [None]: us-east-1
Default output format [None]: json
```

### Step 3: Verify AWS Connection

```powershell
# Test AWS connection
aws sts get-caller-identity
```

Expected output:
```json
{
    "UserId": "AIDACKCEVSQ6C2EXAMPLE",
    "Account": "123456789012",
    "Arn": "arn:aws:iam::123456789012:user/your-username"
}
```

---

## Install PostgreSQL Client (psql)

You'll need this to connect to RDS and run migrations.

### Option 1: Chocolatey
```powershell
# Run as Administrator
choco install postgresql16 -y
```

### Option 2: Direct Download
1. Visit: https://www.postgresql.org/download/windows/
2. Download "Windows installers" by EDB
3. Run installer
4. Install components:
   - ✅ Command Line Tools (includes psql)
   - ❌ PostgreSQL Server (not needed if you already have it)
   - ❌ Stack Builder (optional)
5. Add to PATH: `C:\Program Files\PostgreSQL\16\bin`

### Verify psql Installation
```powershell
psql --version
# Expected: psql (PostgreSQL) 16.x
```

---

## Summary Checklist

After completing installation:

- [ ] AWS CLI installed (`aws --version` works)
- [ ] Terraform installed (`terraform version` works)
- [ ] AWS credentials configured (`aws sts get-caller-identity` works)
- [ ] psql client installed (`psql --version` works)

**Next step:** Deploy infrastructure with Terraform!
