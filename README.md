# v2rayN NoPromotion + Quick Split Rule

Unofficial Windows x64 WPF builds of [2dust/v2rayN](https://github.com/2dust/v2rayN), with the Promotion toolbar entry hidden and a simpler way to add routing exceptions.

## Quick Split Rule

Open **Settings -> Quick Split Rule**. The dialog edits the currently active, enabled, unlocked routing profile; it does not create a separate routing system.

Choose a target, Direct or Proxy, and where the new rule should be inserted. Review the native rule preview, then select **Add rule**.

| Type | Example | Matching |
| --- | --- | --- |
| Domain / website | `nalog.ru` or `https://chatgpt.com/` | Exact hostname, or hostname plus subdomains. Pasted URL paths and ports are not matched. |
| IP / CIDR | `1.1.1.1`, `192.168.1.0/24`, `fd00::/8` | IPv4 and IPv6. CIDR host bits are normalized in the preview. |
| Windows process | `Code.exe` | Executable name; requires TUN. Child processes with different executable names need their own rules. |

The default position is **before the first general catch-all**, preserving earlier rules. Earlier rules can still take precedence. Select **First rule** for a highest-priority exception in this profile. A preview explains the chosen position.

The dialog validates inputs and rejects equivalent enabled rules. It preserves existing rule objects, their IDs, order, and unknown JSON fields. A conditional SQLite update prevents a stale dialog from overwriting newer rules and changes only the rule list and count, not profile settings. No database schema or application dependencies are added.

**Reload after saving** is optional and enabled by default. Reloading briefly interrupts active connections. It uses the existing v2rayN reload operation, not a new core lifecycle implementation.

### Backups and undo

Before saving, the previous rule array is written to `quick-split-backups` inside v2rayN's configuration directory. A backup failure prevents the save. The saved backup path is displayed in the notification/log.

To undo one added rule, remove its **Quick Split:** entry in normal Routing Settings. To restore the complete previous rule list, edit the same profile in Routing Settings, import the backup JSON, choose **replace rather than append**, confirm, and reload. A full restore also removes any other edits made since that backup.

### Scope and limitations

The first version is English-only and supports one domain, IP/CIDR, or executable name per operation. Use the full routing editor for `geosite:`, `geoip:`, complex conditions, paths, and other advanced rules. A domain rule covers that hostname, not necessarily every supporting domain used by a website. Direct bypasses the proxy; it is not an OS-level TUN route exclusion. Process matching depends on the configured core/TUN setup. Custom full configuration files and locked routing profiles are not edited.

## Automated releases

GitHub Actions checks the latest official stable release daily at **06:17 UTC**, and supports manual builds. It checks out the exact tag and submodules, applies `patches/series` in order, runs validation/persistence tests, builds the WPF project, and validates the package before publishing. Any failed patch, test, build, or package check prevents publication.

The package starts as the matching official `v2rayN-windows-64.zip`. Only `v2rayN-windows-64/v2rayN.exe` is replaced; all other file contents, including proxy cores, are checked against the official archive. No full publish-directory overlay or whole-archive recompression is used.

`revision.txt` identifies our custom revision, for example `7.24.9-nopromotion.2`. Published revisions are not silently replaced. Increment the revision for another custom feature or fix on the same upstream version. The original NoPromotion-only revision remains available as a fallback.

Each release includes the binary ZIP, patched source ZIP (including submodule source and the builder recipe/tests), and SHA-256 files. GitHub's automatically generated source archives contain only this builder repository; use the attached patched-source ZIP for application source.

Automated checks do not substitute for an interactive Windows UI test or testing a user's live network configuration. Future upstream changes can require patch maintenance; scheduled Actions can also be delayed or disabled by GitHub for an inactive public repository.

## Updates and Windows warnings

The executable is **unsigned**, so Windows may show SmartScreen. Do not disable Windows protection globally to install it. Check the release and its hash before choosing whether to trust it.

The built-in application updater is unchanged and installs official builds, which can remove our customizations. Install custom builds from this repository's Releases page. Back up your current configuration before upgrading; do not overwrite your saved configuration with an empty setup.

## Development

Use the matching .NET 10 SDK and check out the desired upstream release with submodules into `upstream` beside this README. From the upstream directory apply each patch named in `../patches/series`, in order, with `git apply --check` followed by `git apply`.

From the builder root run:

```powershell
dotnet run --project tests/QuickSplitRule/QuickSplitRule.Tests.csproj -c Release -p:PublishSingleFile=false -p:SelfContained=false
dotnet publish upstream/v2rayN/v2rayN/v2rayN.csproj -c Release -r win-x64 -p:SelfContained=true -p:EnableWindowsTargeting=true -o custom-build
```

See `.github/workflows/build.yml` for source archiving, SDK selection and complete release packaging. The tests use the actual upstream routing entities and SQLite library, with a temporary database; they do not use your v2rayN configuration.

## License and attribution

v2rayN is developed by 2dust and distributed under GPL-3.0. The patches and corresponding modified application source are distributed under the same license; upstream license notices are retained. Bundled third-party components retain their own licenses. These builds are not official v2rayN releases and are not signed or endorsed by upstream.
