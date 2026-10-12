# Releasing azlocal

The server is distributed as a Docker image. Applications use the NuGet client library. The former global CLI was retired after the NuGet-only `0.1.0-alpha.1` release; it is not built or published in future releases.

## Release artifacts

| Artifact | Purpose |
| --- | --- |
| `clifftech123/azlocal:<version>` | Linux emulator server image |
| `AzLocal.Client` | DI registration and local Azure SDK setup |
| `AzLocal.Core` | Required client dependency |

The image and both packages use the version determined by MinVer from the release tag. Only stable releases update the Docker `latest` tag. Initial Docker images support `linux/amd64`.

## GitHub account configuration

NuGet uses Trusted Publishing. In NuGet, configure a GitHub Actions policy with repository owner `Clifftech123`, repository `azlocal`, workflow file `release.yml`, and no environment. Allow creating and updating `AzLocal.Client` and `AzLocal.Core`; choose the user or organization owning the packages.

Set `NUGET_USER` as a repository secret or variable containing your individual NuGet profile username, not an email address. A stored NuGet API key is not needed.

For Docker Hub, configure:

| Setting | Location | Value |
| --- | --- | --- |
| `DOCKERHUB_USERNAME` | Secret or variable | Docker Hub username |
| `DOCKERHUB_NAMESPACE` | Secret or variable | Owner of the `azlocal` repository |
| `DOCKERHUB_TOKEN` | Secret | Docker Hub access token with push permission |

The workflow accepts secrets or variables for username/namespace and prefers secrets if both exist. Never place tokens in source files or release notes.

## Publish a prerelease

1. Wait for all CI checks on `develop`: native builds/tests and client package compilation on Windows, Linux, and macOS, plus live Docker SDK and restart-persistence checks.
2. Review and merge the change into `main`.
3. Create a GitHub release targeting the merged commit, with a new tag such as `v0.1.0-alpha.2`.
4. Enable **Set as a pre-release** and describe changes and limitations.
5. Publish the release and watch the **Release** workflow.

The source commit must be merged into `main`. The workflow checks that the tag matches MinVer and the prerelease flag matches the version suffix. Docker and NuGet uploads happen only after all test jobs pass.

The Docker publisher uploads the same image that passed SDK and persistence tests. NuGet uploads Core first, then Client, and attaches the packages to the GitHub release. Confirm the public image tag and NuGet indexing, then check installation using those published artifacts. Docker Hub's overview can use [the Docker guide](DOCKER.md).

Do not reuse a published version or move its tag. Release fixes as a new alpha version. Retrying a failed NuGet upload skips existing versions. Preserve already published packages as release history.

## Stable releases

After feedback and validation, create a stable tag such as `v0.1.0`, with the GitHub prerelease option disabled. Stable images also update `latest`. A stable version label does not change an operating system's application-control policy; server distribution is Docker-only.

## Automatic versioning

MinVer 6.0.0 is a private build dependency. Shared configuration is in `Directory.Build.props`, and dependency versions are managed in `Directory.Packages.props`.

- A tagged commit produces that exact version without the `v` prefix.
- Untagged commits produce development prerelease versions with commit height.
- After a stable tag, the patch version advances automatically.
- Before the first tag, builds start in the `0.1` range.

CI fetches full Git history. Docker builds receive the calculated version as a build argument because their context excludes Git metadata. No project version fields need editing.

## Build and test without publishing

Use **Run workflow** for **Release**, selecting a branch or tag. Manual runs build and test artifacts but do not publish.

Locally, with .NET 10, PowerShell 7, and Docker:

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --no-restore
$version = dotnet msbuild src/AzLocal.Client -target:MinVer -getProperty:Version -nologo
./eng/Pack.ps1 -NoBuild
./eng/Test-Packages.ps1 -PackageDirectory "artifacts/packages/$version" -Version $version
./eng/Test-Docker.ps1 -Version $version
```

Client package checks install dependencies into a fresh app outside the checkout and compile it. Docker checks consume freshly built NuGet packages in a Linux application container, exercise SDK operations with pinned HTTPS, restart the server, and check persistent state and blobs. They do not alter host certificate trust.

## Troubleshooting publication

- NuGet login failure: verify `NUGET_USER`, policy ownership, repository, workflow filename, scopes, and policy activity.
- Docker Hub login failure: verify the username and access token.
- Docker push failure: verify namespace/repository ownership and token push permission.
- Source validation failure: merge the release commit into `main`.
- Build/test/installation failure: fix the change, rerun CI, and merge before releasing.

Do not disable failing tests to publish.
