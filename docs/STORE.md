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
- **Screenshots** — at least one per listing language, 1366x768 or larger. Generate them with
  `build/Capture-StoreScreenshots.ps1` (see below).
- **Category** — Utilities & tools; secondary category Productivity.
- **Product declarations** — tick "supports purchases but does not use the Microsoft Store
  commerce system": the Donate button goes to Ko-fi, and Store policy 10.8.2 requires that
  third-party payments, donations included, are declared.
- **Certification notes** — worth stating that the app is a plain offline file utility and
  needs no account, so the reviewer does not go looking for a sign-in.

## 5. Store listings in all 21 languages

The listing text for every language lives in
[`docs/store/listing-text.json`](store/listing-text.json). Partner Center takes it as a CSV
import, together with the screenshots.

1. Build the app (Debug is fine), then capture the screenshots. The app window pops up 42
   times; leave the machine alone until it finishes.

   ```powershell
   ./build/Capture-StoreScreenshots.ps1 -Files (1..4 | % { "F:\dev\test1\csv\sales-2024-0$_.csv" })
   ```

   Output: `artifacts/store-screenshots/<code>-light.png` and `<code>-dark.png`.

2. In Partner Center, on the submission, choose **Export listings** and save the CSV (for
   example as `artifacts/partner-center-listing-export.csv`). Always start from a fresh export
   of the current submission.

3. Build the import folder:

   ```powershell
   ./build/New-StoreListing.ps1 -Template artifacts\partner-center-listing-export.csv -LocalizedJapaneseTitle
   ```

   This writes `artifacts/store-listing/` with `listingData.csv` and the 42 screenshots, and
   refuses to write anything if a text breaks a Partner Center limit.

4. In Partner Center choose **Import listings → Import folder** and pick
   `artifacts/store-listing` itself.

What went wrong the first time, so it does not again (see
[Import and export Store listings](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/import-and-export-store-listings)):

- **Image paths start with the name of the uploaded folder**: `store-listing/en-light.png`.
  Neither `en-light.png` nor `images/en-light.png` works. A wrong path does not produce a
  per-field error; the whole CSV is rejected with "this .csv file could not be processed" and
  an empty error list.
- **Nothing is saved until the whole file is error-free**, including the fields that were
  fine. When an import fails, split it: import the CSV alone (**Import .csv**) with the image
  fields empty to test the text, then the folder.
- **Only one .csv file in the folder.**
- **Keep Partner Center's own CSV style**: UTF-8 with BOM, quotes only where needed, the
  Field, ID and Type columns and the padding rows at the end unchanged. PowerShell's
  `Export-Csv` quotes every value; the script writes the file itself for that reason.
- **The Japanese title (てくてくファイル結合) must be reserved** under App management → Manage
  app names before the import. Every other language uses TekuTeku File Merge.
- After a successful folder import, a new export shows the images as Partner Center URLs.
  Those URLs can be reused in later imports instead of uploading the files again.

This app's Store identity (not secret; it ends up in every package):

| Field | Value |
| --- | --- |
| Store ID | 9MSSHDP4MJZK |
| Package/Identity/Name | 9B6C9F60.TekuTekuFileMerge |
| Package/Identity/Publisher | CN=E5A55C73-7E5B-4BF9-B37E-C562F23A3A5E |
| PublisherDisplayName | Hiroyura |

## 6. Automating it

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
