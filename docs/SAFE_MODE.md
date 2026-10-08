# Safe Mode and Windows Sandbox crash diagnosis

As of October 9, 2026, the `develop` branch includes an opt-in Safe Mode for TableCloth. It prepares a Windows Sandbox without host folder mappings. Release availability depends on a build containing this change.

This guide covers the execution policy, features that become unavailable, guest downloads and diagnostics, crash comparisons, and information for a bug report.

Read the execution policy first, then use the comparison steps to distinguish a host crash from a guest setup failure. The feature implements the scope in [issue #311](https://github.com/yourtablecloth/TableCloth/issues/311).

> Status checked on October 9, 2026. Safe Mode is available in this development branch. Preview behavior may change before a stable release.

## Execution without host folder mappings

Open **Options > Sandbox session > Use Safe Mode**. The Quick Start screen shows **Run mode: Safe Mode**. A confirmation explains the limitations before each launch. Quick Start, catalog launches, installed deep links, AI catalog launches, and existing command-line launch routes use the common builder policy. Browser-only launches retain their isolated Edge policy.

| Setting | Safe Mode policy | Effect |
| --- | --- | --- |
| Host folders | No `MappedFolders` element | No App, Data, NPKI, custom folder or host .NET runtime mapping |
| Clipboard and devices | Explicit Disable for clipboard, printer, camera and microphone | No copy/paste between host and guest or device redirection |
| GPU | Explicit Disable | Software rendering |
| Network | Explicit Enable | Guest downloads and websites remain available |

Normal-mode settings remain saved. Turning Safe Mode off restores their use. Safe Mode still uses Windows Sandbox virtualization and the host's underlying services. The [Windows Sandbox configuration reference](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file) describes these settings.

## Guest features and downloads

The generated WSB downloads the same preparation script used by Express inside the guest. The guest downloads `SporkBootstrap_x64.exe` or `SporkBootstrap_arm64.exe` from the official latest stable release. The bootstrapper downloads, extracts and launches Spork. A host Preview can therefore launch a different stable guest version. The installed host binaries and catalog cache do not enter the guest. See the [Express design](EXPRESS_BOOTSTRAPPER_DESIGN.md).

Host certificates and ZScaler roots are unavailable. A managed network that requires those roots can reject HTTPS downloads. Safe Mode does not disable certificate validation to bypass that failure. Use an authentication method that does not require host files, such as a supported mobile authentication flow. Guest files, favorites and history disappear when the sandbox closes.

Host session options such as idle shutdown are not transferred. The initial setup honors the public DNS fallback preference; the downloaded stable guest applies its own defaults. Disabling the fallback keeps the initial DNS configuration intact.

## Setup logs and failure boundaries

The guest desktop contains `tablecloth-safe-mode.log` with the requested mode, architecture, launcher source and actual Spork executable version. `spork-bootstrap.log` records package download URLs and bootstrap results. The host application log records the run mode and guest source. These logs describe different stages; successful WSB generation alone does not prove that a guest download or launch completed. The [bootstrapper source](../src/Spork.Bootstrapper/Program.cs) implements package diagnostics.

The setup console keeps download or preparation failures visible. The bootstrapper retains its retry and close controls. Before closing the sandbox, record the guest logs through a permitted reporting method; Safe Mode deliberately offers no shared Data folder or clipboard export. Report a guest download error separately from a blue-screen crash of the host.

## Comparisons that preserve the evidence

The diagnostic account in [issue #313](https://github.com/yourtablecloth/TableCloth/issues/313) associates four dumps with a NULL read at `nrpmonka.sys + 0x58E1` in a VSMB file-open path. The reporter described successful standalone Sandbox, installation-mode execution after stopping the NURILAB driver, and Express execution. The exact stopping operation, NURILAB state during Express testing, and the particular triggering shared folder remain unconfirmed. An absence of earlier crashes on another PC does not establish a controlled comparison.

If the security product supports a documented pause, record that setting and restore protection after the comparison. A product's pause button does not establish that its kernel driver unloaded. Do not force driver termination, unloading or removal as a diagnostic step. Microsoft documents the separate [minifilter load and unload lifecycle](https://learn.microsoft.com/en-us/windows-hardware/drivers/ifs/loading-and-unloading).

Safe Mode can still crash. That result alone does not identify networking as the cause. A network-disabled diagnostic WSB cannot download Spork; reaching no download stage does not count as a successful TableCloth run. TableCloth does not provide a network-disabled application mode. If networking is established as the trigger, the current Windows and security-product environment cannot run TableCloth until compatibility is resolved with the vendor or Windows support.

## Comparison record and reporting checklist

Use the [bug report template](../.github/ISSUE_TEMPLATE/------.md) to preserve the conditions and stage of each result.

1. Record the exact Windows build, CPU architecture, TableCloth host version, guest version, security-product version and driver version.
2. Record standalone Windows Sandbox, normal TableCloth, official Express WSB and Safe Mode outcomes separately.
3. Record the security-product protection state for each run, including Express and Safe Mode comparisons.
4. Record whether failure occurred before the guest opened, during download, during Spork preparation or during website use.
5. Preserve the stop code, timestamp and a new minidump for a host crash. Share dumps privately through an agreed channel because they can contain sensitive data.
6. Record guest setup logs for download failures and any same-condition comparison on another PC.

Safe Mode removes selected host integration paths while keeping online site use available. Driver compatibility investigation can continue separately from an immediate Safe Mode comparison.

If the guest runs successfully under the recorded protection state, Safe Mode can provide a temporary execution path within its feature limits. If host crashes continue, keep the comparison evidence and investigate Windows and security-product compatibility before resuming use.
