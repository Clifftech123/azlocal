# Releasing azlocal

The first public testing version is `0.1.0-alpha.1`. Publish prereleases while collecting feedback, then publish `0.1.0` when the supported operations and installation flow are ready for a stable release.

## Packages

Each release publishes the same version of three packages:

| Package | Purpose |
| --- | --- |
| `azlocal` | .NET tool with the bundled emulator host |
| `AzLocal.Client` | DI registrations and local Azure SDK client setup |
| `AzLocal.Core` | Required dependency of the client library |

Host internals and test projects are not published as separate packages. The CLI bundles its host dependencies. NuGet installs Core automatically when an application references Client.

## Before the first publication

1. Wait for the `develop` CI workflow to pass on Windows, Linux, and macOS. It builds, runs the test suites, packs all three packages, and installs them into a fresh application for live SDK checks.
2. Review and merge the `develop` pull request into `main`. Release tags must point to commits merged into `main`.
3. Create a NuGet.org API key with permission to create and push `azlocal`, `AzLocal.Client`, and `AzLocal.Core`. A key scoped only to `AzureStorage.Standard.*` cannot publish these packages.
4. Add that key as the repository Actions secret `NUGET_API_KEY` in [GitHub settings](https://github.com/Clifftech123/azlocal/settings/secrets/actions). Do not put the key in source files, a workflow, a release description, or an issue.

Alternatively, run `gh secret set NUGET_API_KEY --repo Clifftech123/azlocal` locally and enter the key at its prompt. This repository currently uses an API-key publishing workflow; NuGet trusted publishing can be added later.

## First prerelease

Create a GitHub release from `main` with:

- Tag: `v0.1.0-alpha.1`
- Title: `azlocal 0.1.0-alpha.1`
- The **Set as a pre-release** option enabled
- Notes describing the supported operations, installation commands, and known limitations

Publish the GitHub release when you are ready to upload the packages to NuGet. Saving a draft does not publish packages. The `NuGet release` workflow validates the version, tests and installs the packages on all three operating systems, and publishes only after every platform passes.

The workflow pushes Core first, then Client, then the CLI, and attaches the package files to the GitHub release. Check that the workflow succeeds and allow time for NuGet indexing before testing installation from the public feed.

```sh
dotnet tool install --global azlocal --version 0.1.0-alpha.1
dotnet add package AzLocal.Client --version 0.1.0-alpha.1
```

NuGet package versions cannot be overwritten. If a published prerelease needs a fix, publish `0.1.0-alpha.2` rather than changing the existing tag. A failed upload can be retried; already uploaded packages are skipped.

## Stable release

After feedback and passing CI, update the changelog and prepare the next version on `develop`. Merge it into `main`, then create and publish a GitHub release tagged `v0.1.0` with the prerelease option disabled. The package version comes from the release tag, so a stable tag produces stable package versions even when the development default has an alpha suffix.

## Automatic versioning

[MinVer](https://github.com/adamralph/minver) calculates assembly and package versions from Git tags. All projects share the same settings in `Directory.Build.props`, and the MinVer dependency is private to the build.

- A commit tagged `v0.1.0-alpha.1` produces package version `0.1.0-alpha.1`.
- A commit tagged `v0.1.0` produces stable package version `0.1.0`.
- Untagged commits get development prerelease versions that include the number of commits since the selected tag. After a stable tag, the patch version advances automatically.
- Before the first tag, development versions start in the `0.1` range.

Choose a tag when cutting a release; there are no project version numbers to edit. Dependency versions still belong in `Directory.Packages.props`. CI fetches the full Git history so MinVer can find tags. Release validation checks that the calculated version matches the published release tag.

## Build and test a release without publishing

Use the `NuGet release` workflow's **Run workflow** button and select a branch or tag. Manual runs calculate its version from Git and build and test artifacts but never upload to NuGet. This can be run on `develop` before merging.

To check locally, use PowerShell 7 and the .NET 10 SDK:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --no-restore
$version = dotnet msbuild src/AzLocal.Client -target:MinVer -getProperty:Version -nologo
./eng/Pack.ps1 -NoBuild
./eng/Test-Packages.ps1 -PackageDirectory "artifacts/packages/$version" -Version $version
```

The installation check runs outside the checkout, installs the tool and client from the built packages, starts a host with temporary HTTPS and SQLite settings, and verifies Blob Storage, Key Vault, Service Bus, ARM, managed identity, and DI. It creates no persistent certificate trust settings and prints its temporary log directory.

The optional `Pack.ps1 -Version` argument checks an expected version against Git; it never overrides MinVer. To build an exact release version, check out that release tag first.

## If publication fails

- Missing key: add `NUGET_API_KEY` in the repository's Actions secrets.
- NuGet authorization failure: check that the key is valid and can publish all three package IDs.
- Version/prerelease mismatch: use a suffixed version for a prerelease and an unsuffixed version for a stable release.
- Source check failure: merge the release commit into `main` before creating the release.
- Build, test, or install failure: fix it on `develop`, rerun CI, and merge the fix before retrying a release.

Do not disable failing tests to publish a release. Stable releases describe a dependable local developer tool, not a replacement for Azure's production services.
