# Publishing to the Microsoft Store

Everything the repository needs for a Store submission is already here. What is missing is the
identity, which only Partner Center can give you.

## 1. Reserve the name

1. Sign in to [Partner Center](https://partner.microsoft.com/dashboard) with a developer
   account (one-off fee: about 19 USD for an individual, 99 USD for a company).
2. **Apps and games → New product → MSIX or PWA app**.
3. Reserve the name, for example `FileMerge`. If it is taken, pick a variant; the name shown
   on the tile comes from the manifest and does not have to match.

## 2. Copy the identity values

Open **Product management → Product identity**. Three values matter:

| Partner Center field | Script parameter |
| --- | --- |
| Package/Identity/Name | `-IdentityName` |
| Package/Identity/Publisher | `-Publisher` |
| Package/Properties/PublisherDisplayName | `-PublisherDisplayName` |

## 3. Build the package

```powershell
./packaging/msix/Build-Msix.ps1 `
    -IdentityName '12345Publisher.FileMerge' `
    -Publisher 'CN=ABCDEFGH-1234-5678-9012-ABCDEFGHIJKL' `
    -PublisherDisplayName 'Your Publisher Name' `
    -Architecture x64 `
    -Version 1.0.0
```

This writes `artifacts/FileMerge-1.0.0.0-x64.msix`.

Repeat with `-Architecture arm64` and upload both; the Store serves whichever fits the device.

Requires the Windows 10/11 SDK for `makeappx.exe`. The script searches the usual install
locations and reports clearly if it cannot find it.

**Leave the package unsigned.** Partner Center signs submissions with the certificate tied to
your reserved identity. A package you signed yourself will be rejected.

### Testing the package before submitting

To install it on your own machine you *do* need a signature, with a certificate your machine
trusts:

```powershell
# One-off: create a self-signed certificate whose subject matches -Publisher exactly
$cert = New-SelfSignedCertificate -Type Custom -Subject 'CN=FileMerge' `
    -KeyUsage DigitalSignature -FriendlyName 'FileMerge test' `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

$password = Read-Host -AsSecureString
Export-PfxCertificate -Cert $cert -FilePath test.pfx -Password $password

# Trust it, then build a signed package with the matching publisher
Import-PfxCertificate -FilePath test.pfx -CertStoreLocation 'Cert:\LocalMachine\Root' -Password $password

./packaging/msix/Build-Msix.ps1 -Publisher 'CN=FileMerge' `
    -Sign -CertificatePath .\test.pfx -CertificatePassword $password

Add-AppxPackage .\artifacts\FileMerge-1.0.0.0-x64.msix
```

The `-Publisher` value must match the certificate subject character for character, or Windows
refuses the install. Remove the test certificate from the Trusted Root store when you are done.

## 4. Submission checklist

- **Age rating** — answer the questionnaire. FileMerge has no user-generated content, no ads
  and no data collection, so it rates as suitable for all ages.
- **Privacy policy** — required whenever a submission declares any capability. FileMerge only
  declares `runFullTrust` and sends nothing anywhere, but a URL is still required. A page
  stating that the app collects and transmits no data is enough; GitHub Pages works.
- **Store listing** — needed per language you list. The app itself ships 21 languages; you can
  start with one listing and add more later.
- **Screenshots** — at least one, 1366x768 or larger. There are usable ones in
  [`docs/images`](images).
- **Category** — Utilities and tools → File managers.
- **Certification notes** — worth stating that the app is a plain offline file utility and
  needs no account, so the reviewer does not go looking for a sign-in.

## 5. Automating it

[`.github/workflows/release.yml`](../.github/workflows/release.yml) builds the MSIX on every
`v*` tag. Set these repository variables (Settings → Secrets and variables → Actions →
Variables):

- `MSIX_IDENTITY_NAME`
- `MSIX_PUBLISHER`
- `MSIX_PUBLISHER_DISPLAY_NAME`

The workflow attaches the `.msix` to a draft GitHub Release; upload it to Partner Center from
there. Fully automated submission is possible through the
[Store submission API](https://learn.microsoft.com/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services),
which needs an Azure AD application with Partner Center access — worth setting up once
releases become frequent.

## Store rules this app already satisfies

- No installer: MSIX is the installer.
- No writing outside the package container or the user's chosen files.
- Runs without administrator rights.
- Works offline, with no account.
- Self-contained: the .NET runtime is inside the package, so the Store never has to install a
  dependency framework.
