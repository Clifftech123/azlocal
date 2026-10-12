# Windows Application Control

The `0.1.0-alpha.1` CLI can install successfully but fail to start on Windows systems enforcing Smart App Control or another Application Control policy:

```text
Program 'azlocal.exe' failed to run:
An Application Control policy has blocked this file.
```

This happens before azlocal code runs. Certificate trust commands, emulator settings, and running the command as administrator do not resolve a signing-policy block.

## Confirm the cause

Open Event Viewer and navigate to:

**Applications and Services Logs → Microsoft → Windows → CodeIntegrity → Operational**

Look for events that name `azlocal.exe` or an azlocal assembly. Event `3077` is an enforced Application Control block; related events can identify the signature or policy involved. See [Microsoft's event reference](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/operations/event-id-explanations).

The issue was reproduced on Windows with Smart App Control enabled. The global-tool launcher had no embedded Authenticode certificate, and the enforced `VerifiedAndReputableDesktop` policy rejected it. Passing tests on GitHub-hosted Windows runners does not verify compatibility with this policy.

## Release requirements

NuGet Trusted Publishing authenticates uploads to NuGet. It does not Authenticode-sign the installed Windows executable or the application's assemblies. The HTTPS development certificate is also separate from publisher signing.

A compatible Windows distribution needs binaries that satisfy the device's signing policy and installation testing with that policy enabled. Microsoft recommends signing with a trusted provider, including [Artifact Signing](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control).

For a .NET global tool, the SDK can generate the command launcher during installation. Signing only the `.nupkg` is insufficient: the launcher and loaded binaries must be considered too. A future signing setup needs to verify the installed result, not just the uploaded archive.

On an organization-managed device, ask the administrator to review the application and provide an approved deployment or policy if appropriate. Do not disable Application Control as an installation step. No policy or certificate-store changes are made by this troubleshooting guide.

This compatibility issue remains open for the published alpha release. It must be resolved and verified before claiming support for Windows systems with enforced App Control.
