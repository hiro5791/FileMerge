# winget manifest

`manifests/h/hiro5791/FileMerge/<version>/` mirrors what is submitted to
[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs), so the package can be
installed with `winget install hiro5791.FileMerge`.

For a new release: copy the latest version folder, change `PackageVersion`, `InstallerUrl`,
`InstallerSha256` (`Get-FileHash` of the published setup .exe) and `ReleaseDate`, run
`winget validate --manifest <folder>`, then open a pull request that adds only that folder to
winget-pkgs. See "Submit your manifest to the repository" on Microsoft Learn.

Checked for 1.0.1: `winget validate` passes, and the installer installs and uninstalls silently
per user (`/VERYSILENT /CURRENTUSER`), registering ProductCode
`{7C1E6B52-9F0A-4D8E-B3A1-5E2C4D6F8A90}_is1` under publisher `hiro5791`.
