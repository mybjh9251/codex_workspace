# PetaLinux Package List Test Plan

## Sources Checked

- AMD UG1144 2025.1 Installation Requirements states that PetaLinux 2025.1 supports Ubuntu Desktop/Server 22.04.2, 22.04.3, 22.04.4, and 22.04.5 LTS.
- AMD UG1144 2025.1 also states that required packages are listed in the release notes and that PetaLinux requires standard development tools and libraries on the Linux host workstation.
- Xilinx PetaLinux 2017.3 is treated as the legacy Ubuntu 16.04 xenial host package scenario for this downloader smoke test.

## Test Case: PetaLinux 2017.3

- Ubuntu Version: `16.04 xenial`
- Architecture: `amd64`
- Components: `main universe`
- Pockets: `release updates security`
- Package List: `test-data/package-lists/petalinux-2017.3-ubuntu-host.txt`
- Expected behavior:
  - Kakao/current Ubuntu metadata may fail for EOL xenial.
  - Resolver should fallback to `https://old-releases.ubuntu.com/ubuntu/`.
  - Resolved `.deb` list should be written to `manifest.json` and `download-report.csv`.

## Test Case: PetaLinux 2025.1

- Ubuntu Version: `22.04 jammy`
- Architecture: `amd64`
- Components: `main universe`
- Pockets: `release updates security`
- Package List: `test-data/package-lists/petalinux-2025.1-ubuntu-host.txt`
- Expected behavior:
  - Kakao mirror should be tried first.
  - Ubuntu official fallback should be used only if Kakao metadata or package download fails.
  - Resolved `.deb` list should be written to `manifest.json` and `download-report.csv`.

## Current Environment Result

WSL Ubuntu is not installed and is not required.

The Windows native metadata resolver smoke was executed with:

- Ubuntu Version: `24.04 noble`
- Architecture: `amd64`
- Components: `main universe`
- Pockets: `release updates security`
- Requested Package: `curl`
- Result: `32` package files resolved
- Unresolved dependencies: `0`
- Repository result: Kakao mirror succeeded without official fallback

The same smoke was executed for `arm64`:

- Kakao mirror returned `404` for the Ports metadata path.
- Resolver automatically retried with `https://ports.ubuntu.com/ubuntu-ports/`.
- Result: `32` package files resolved
- Unresolved dependencies: `0`
- Official fallback: used

Repository selection was also verified for `amd64`:

- `Kakao Mirror (Ubuntu fallback)`: 32 packages, unresolved 0
- `Ubuntu Official`: Kakao lookup skipped, 32 packages, unresolved 0

Runtime output contract:

- The UI exposes only `Download` as the primary operation button.
- `Download` performs resolution and package download as one operation.
- Runtime output is fixed to `<executable directory>\Download`.
- `.deb` files and report files are written directly under `Download`.
- Full `curl` download smoke wrote 32 `.deb` files directly under the output folder.
- Generated checksum entries contained no `debs/` prefix.

Debian version comparison regression:

- Requested packages: `curl`, `git`
- Previous failure: `git-man (<< 1:2.43.0-.)` was incorrectly reported as unresolved.
- Cause: numeric characters were ordered like punctuation during Debian non-digit comparison.
- Fixed result:
  - Resolved packages: `52`
  - Unresolved dependencies: `0`
  - Downloaded `.deb` files: `52`

Smoke command:

```powershell
dotnet run --project .\tests\UbuntuPackageDownloader.Smoke\UbuntuPackageDownloader.Smoke.csproj -c Release -- amd64
dotnet run --project .\tests\UbuntuPackageDownloader.Smoke\UbuntuPackageDownloader.Smoke.csproj -c Release -- arm64
dotnet run --project .\tests\UbuntuPackageDownloader.Smoke\UbuntuPackageDownloader.Smoke.csproj -c Release -- amd64 official
```
