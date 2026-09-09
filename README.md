# v2rayN NoPromotion

Automated Windows x64 builds of official [2dust/v2rayN](https://github.com/2dust/v2rayN) releases with the **Promotion** toolbar entry hidden from the WPF interface.

## What changes

Only the Windows WPF toolbar is changed:

- the `Promotion` toolbar entry is hidden;
- its adjacent separator is hidden;
- routing, proxy cores, configuration logic, networking logic, and the upstream version number are left unchanged.

The customization is intentionally kept as one small patch in [`patches/remove-promotion-ui.patch`](patches/remove-promotion-ui.patch).

## Automatic builds

GitHub Actions checks the latest official v2rayN release every day. When a new upstream release appears, the workflow:

1. checks out that exact upstream release tag;
2. applies the NoPromotion patch (and stops if it no longer applies cleanly);
3. builds the official Windows x64 WPF project;
4. uses the official release package as the base so the bundled proxy cores remain the same as upstream;
5. publishes a `NoPromotion` GitHub release with the Windows ZIP, the patch, and a SHA-256 file.

The workflow can also be run manually for a specific upstream release tag.

## Scope

This repository deliberately does **not** pin, downgrade, or otherwise modify Xray, sing-box, or other bundled cores. Core versions come from the corresponding official v2rayN release package.

## Upstream and license

v2rayN is developed by 2dust and is licensed under GPL-3.0. These are unofficial custom builds. Each release identifies the exact upstream release used, and the complete customization is published as the patch alongside the build.
