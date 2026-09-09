# v2rayN NoPromotion

Automated Windows x64 builds of official [2dust/v2rayN](https://github.com/2dust/v2rayN) releases with the **Promotion** toolbar entry hidden from the WPF interface.

## What changes

Only the Windows WPF toolbar source is changed:

- the `Promotion` toolbar entry is hidden;
- its adjacent separator is hidden;
- routing, proxy cores, DNS, configuration logic, networking logic, and the upstream version number are not modified.

The customization is intentionally kept as one small patch in [`patches/remove-promotion-ui.patch`](patches/remove-promotion-ui.patch).

## How the binary package is produced

v2rayN publishes the Windows WPF application as a self-contained single-file executable. The workflow therefore:

1. downloads the matching official `v2rayN-windows-64.zip`;
2. keeps that official ZIP as the package base;
3. builds the patched WPF application from the exact upstream release tag using the same .NET SDK channel as upstream;
4. replaces **only** `v2rayN-windows-64/v2rayN.exe` inside the official ZIP;
5. validates that the final archive contains the patched executable and refuses to publish if the package size changes unexpectedly.

Every other ZIP entry, including the bundled proxy cores and configuration files, remains from the matching official v2rayN package.

## Automatic builds

GitHub Actions checks the latest official v2rayN release every day. When a new upstream release appears, the workflow:

1. checks out that exact upstream release tag and submodules;
2. verifies that the packaging assumptions still hold;
3. applies the NoPromotion patch and stops if it no longer applies cleanly;
4. creates the corresponding patched-source archive before compilation;
5. builds and validates the Windows x64 WPF package;
6. publishes a `NoPromotion` GitHub release with:
   - the Windows x64 ZIP;
   - SHA-256 for the binary ZIP;
   - the corresponding patched-source ZIP;
   - SHA-256 for the source ZIP;
   - the exact patch.

The workflow can also be run manually for a specific official release tag. Push/manual runs rebuild the current custom release, which allows builder fixes to replace bad assets without creating a fake new upstream version.

## Updates inside v2rayN

The application's built-in updater is intentionally left unchanged and still points to official v2rayN releases. Using the built-in updater may therefore replace the NoPromotion executable with the official one. Install future NoPromotion builds from this repository's Releases page if you want to keep the UI change.

## Scope

This repository deliberately does **not** pin, downgrade, or otherwise modify Xray, sing-box, or other bundled proxy cores. Core versions come from the corresponding official v2rayN release package.

## Upstream and license

v2rayN is developed by 2dust and is licensed under GPL-3.0. These are unofficial custom builds. Each release identifies the exact upstream release used and includes corresponding patched source for the distributed binary.
