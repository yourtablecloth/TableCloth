# Windows Sandbox RDP hosting research archive

On October 23, 2026, the RDP experiment was retired in favor of retaining research documentation on `poc/managed-ai-runtime`. This archive covers `poc/rdp-activex-host` at commit `dcf8cef35d1a8708e1dc8251ae4a08ada7458afd` and the subsequent uncommitted experiments. The destination branch receives documentation and an evidence summary only.

The record covers Avalonia and Native AOT hosting, the private Sandbox broker, session and mapped-asset lifetime, display and keyboard experiments, and qualification limits. Test counts describe historical runs. No tests or VM experiments were rerun for this documentation change.

The following sections distinguish the retained findings, private protocol observations, lifecycle design, host ownership, and measured behavior. See the [Korean archive](rdp-sandbox.ko.md) and the [evidence summary](rdp-sandbox-evidence.json).

The private integration has a specific observation boundary:

> Snapshot: October 23, 2026. Internal-API experiments targeted Windows Sandbox 0.8.107.0 on Windows 11 x64. The private broker and RDP pipe contracts may change with package updates and do not constitute a supported public integration contract.

## Retirement scope and provenance

The destination baseline is `poc/managed-ai-runtime` commit `b403591ccebc3d6e27ef4bc937592e3539e5bc17`. The RDP branch also contains later `develop` changes. Merging that branch would import unrelated work, so no RDP implementation, tests, option UI, package references, resources, or diagnostic `.wsb` files were transferred.

The last committed revision used the CLI for session enumeration and creation. Subsequent uncommitted work replaced lifecycle operations with direct broker RPCs and added product routing, a single-session UI, display updates, and keyboard experiments. Menu names, classes, and diagnostic verbs below identify retired experiments. They do not describe executable features on the destination branch. Its existing AI behavior remains documented in the [Managed AI report](managed-ai-runtime.md).

Existing reports, screenshots, and binaries remain in the local ignored `Artifacts/` tree. They are unavailable from a fresh checkout or the remote repository. The committed [evidence summary](rdp-sandbox-evidence.json) retains selected findings, relative artifact paths, and SHA-256 digests. Digests do not reproduce the original execution. Raw logs, screenshots, and binaries are outside the documentation commit.

## Package-specific broker and transport observations

The later adapter used HTTP/2 with gRPC framing over the per-user `wsandbox\{GUID}` named pipe. The GUID was derived from the MD5 of the UTF-8 user SID. Observed operations were `EnumerateSandboxVMs`, `StartSandbox`, `ShutdownSandbox`, and `GetRdpClientConfig`. If the broker was unavailable, the adapter activated the installed `WindowsSandboxServer.exe` execution alias. This later lifecycle path did not invoke `wsb.exe`.

Start messages placed configuration XML in field 1 and the requested GUID in field 2. Omitting `OwnerProcessId`, field 100, allowed a VM to outlive its viewer. Start and RDP-setting replies contained credentials. The design excluded configuration, raw replies, and credentials from logs, command arguments, and exception messages. Parsing applied a 1 MiB message limit, strict UTF-8, HRESULT checks, GUID checks, and frame validation. Start RPCs had a 120-second timeout; other RPCs had 30 seconds.

Compatibility checks required both an x64 OS and process and exactly one registered package, `MicrosoftWindows.WindowsSandbox_0.8.107.0_x64__cw5n1h2txyewy`. A read-only enumeration and preparation of the actual window's ActiveX interfaces and native pipe connector did not prove a later start would succeed. Desktop attachment depended on broker settings and a private pipe connector in `RdpBase.dll`. The adapter did not enable guest TCP RDP or change firewall settings.

Microsoft's [Remote Desktop ActiveX documentation](https://learn.microsoft.com/en-us/windows/win32/termserv/using-remote-desktop-web-connection) describes the public control. It does not guarantee this private broker or pipe contract. Package-specific observations are identified in the [evidence summary](rdp-sandbox-evidence.json).

## Session identity, uncertain outcomes, and mapped assets

Before stopping a session, the experimental configuration loader captured an immutable `.wsb` snapshot and checked XML, duplicate and unsupported settings, values, and mapped directories. It rejected DTDs and added a missing `WorkingDirectory` only to a transport copy, using the original file's parent directory. The [public WSB configuration reference](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file) describes public options separately from the inspected loader's behavior.

Product-routing experiments kept direct `.wsb` file association as the default. The optional embedded route required preflight; unsupported new-session configurations selected file association before launch. Restrictions covered `ProtectedClient`, video input not explicitly disabled, TCP transport, and unqualified display features. The internal configuration loader rejected enabled `ProtectedClient`. When configuration loading succeeded and a surviving session existed, some unsupported viewer configurations still allowed Continue existing or Cancel while disabling Stop and restart. The video restriction reflected the inspected package loader's internal default, rather than a universal public default. Shell launch used the generated file itself with `UseShellExecute=true` and `Verb="open"`.

After an internal start attempt, the design prohibited shell fallback and automatic start retry. Timeout, cancellation, lost replies, parsing errors, and desktop-connection failures could occur after VM creation. It published the requested GUID before dispatch and blocked another start until that same running GUID was observed. An explicit broker `CO_E_APPSINGLEUSE` rejection cleared that request's pending guard without triggering shell fallback. Stop completed only after enumeration confirmed the exact GUID absent. These guards survived viewer closure within the TableCloth process but were not persisted across process restarts.

The product experiment supported zero or one session. Unexpected multiple entries blocked start, connection, and stop. A surviving session prompted Continue existing, Stop and restart with an explicit data-loss confirmation, or Cancel. Replacement meant stopping the exact old GUID, confirming absence, and starting a fresh GUID. It was not atomic and offered no rollback. Without a validate-only broker RPC, a new start could fail after the old session had stopped. Read-only recovery prohibited configuration submission, creation, and termination. A live new-session request with a surviving VM returned `0x800401F6`, `CO_E_APPSINGLEUSE`; an ID collection did not establish concurrent-session support.

Window closing distinguished VM termination, viewer-only closure with the VM left running, and cancellation. Just before launch, staging was marked for retention to avoid removing assets still mapped by a live VM. Uncertain starts retained staging as well. Retained assets could include explicitly selected certificate copies. Automatic reclamation was not implemented. State transitions and historical live findings are recorded in the [evidence summary](rdp-sandbox-evidence.json).

## Avalonia HWND ownership and COM teardown

The ATL ActiveX child was created under the holder HWND passed to `NativeControlHost.CreateNativeControlCore`, then returned as `PlatformHandle(hwnd, "HWND")`. The implementation did not assume the Avalonia top-level HWND was the direct parent. OLE initialization, control creation, COM calls, `DestroyWindow`, and OLE uninitialization stayed on the UI thread.

Raw COM vtable calls avoided dynamic COM and reflection-based wrappers. Explicit `DestroyNativeControlCore` cleanup handled native-window ownership. A later fix treated the observed ActiveX `Disconnect` error `0x80020009` as a cleanup failure, logging only the exception type and HRESULT while continuing HWND destruction and OLE release.

Initial x64 Native AOT evidence showed no loaded CoreCLR, loaded `atl.dll` and `mstscax.dll`, successful `IMsRdpClient10` querying, correct parent and root relationships, four exact View/HWND resize matches, and HWND destruction at exit. Later connected experiments also loaded `RdpBase.dll`. These findings did not qualify transparency, complex clipping, transforms, arbitrary nested layouts, or ARM64. See the [public ActiveX overview](https://learn.microsoft.com/en-us/windows/win32/termserv/using-remote-desktop-web-connection) and [local evidence summary](rdp-sandbox-evidence.json).

## Display updates and observed keyboard behavior

Display experiments used [IMsRdpClient9.UpdateSessionDisplaySettings](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/mt703457%28v%3Dvs.85%29) to change guest resolution. The controller preferred actual ActiveX client pixels and applied render scale once in its logical-size fallback. It coalesced changes for 250 ms, skipped duplicate successful requests, and paused when hidden or minimized.

Transient failures received at most eight attempts per requested size, with delays of 500 ms, 1,000 ms, then a 2,000 ms cap, totaling 11.5 seconds of scheduled waits. Unsupported or failed updates retained [SmartSizing](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings-smartsizing). Display updates did not reconnect the viewer or restart the VM.

On the tested x64 host with one monitor at 100 percent scaling, a guest PerMonitorV2 process measured `1120×751`, `1470×803`, `1120×751`, `920×751`, and `920×571` through `GetSystemMetrics`. The viewer connected once; bounded retry recovered an initial `0x80020009`. A final raw `640×480` sample occurred during viewer closure and was excluded from the five successful connected updates. DPI transitions, minimization, reentrancy, and unsupported APIs had automated coverage only.

Keyboard setup wrote and read back `KeyboardHookMode=1`, `AcceleratorPassthrough=1`, and `EnableWindowsKey=1` on the real ActiveX control before connection. The [documented KeyboardHookMode property](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientsecuredsettings-keyboardhookmode) defines remote shortcut routing; property readback alone does not demonstrate guest input receipt.

After desktop access returned, live clicks showed disconnected and connected plug states and reconnection to the same session. Ordinary `a` reached guest Search. Disconnected `Alt_L+F4` opened the host close confirmation and Cancel retained the viewer. Automated `Alt+Tab` using the generic `Alt` alias activated guest probe B after A, while `Alt_L+Tab` produced no observed activation change. Neither alias of `Alt+F4` produced an observed guest close or host confirmation. Physical Alt+F4 and general shortcut forwarding remain unverified. Resumed client logs reported `1680×1144` at 150 percent scale without an independent guest-resolution measurement.

## Historical test results and qualification limits

The final retained TRX `Counters` were reread during retirement. Counts describe separate revisions and are not cumulative. Earlier passes do not qualify later code. Report paths and SHA-256 digests appear in the [evidence summary](rdp-sandbox-evidence.json).

| Experimental stage | Final passing tests | Live evidence and boundary |
| --- | ---: | --- |
| Initial RDP hosting | 47 | Historical creation from an empty list, mouse input, reconnect, and VM survival after viewer closure |
| Internal lifecycle API | 107 | Focused tests, enumeration and single-instance rejection; separately approved Winver replacement log |
| ActiveX teardown fix | 447 | HWND destruction and exit `0` after Disconnect failure, with the same VM surviving |
| Option and compact layout | 459 | English/Korean headless layout, surviving-session connection and connection-toggle records |
| Icon controls and close choices | 527 | Cancel retained the viewer; Keep running closed it and enumeration found the same session |
| Single-session start confirmation | 605 | Fake-service transitions and Native AOT empty-list teardown; no live creation or termination in this stage |
| Dynamic resolution | 671 | Includes 62 resize cases and four COM checks, plus guest measurements at 100 percent scaling |
| Compact toolbar and keyboard | 685 | Four theme/connection render checks, COM readback, and separate resumed-input records |

The actual configuration-replacement log recorded old-session stop acknowledgement and absence, a fresh GUID's start reply and RDP settings, and a final list with the new session. Contemporary documentation and screenshots also recorded Winver launched from that configuration and viewer attachment. This covered one-session replacement on the inspected package, not concurrent sessions or all product paths.

Keyboard evidence distinguishes two binaries. The initially observed binary had SHA-256 `5B13EB0D6AAFC45105353E95704A3B1511D93B61A51602192917AF0171AE845A`; two source files changed afterward. The final retained binary and resumed record identify `44818723C4D584AE56DEB91BCD2481D9BD332AB9552DF8B8BAF2FFADA8D07B2F`. At retirement, all 497 entries in the final source manifest matched the working files. The resumed record reports `sourceChanged=false` but does not supply an independent hash of the running process image.

The initial keyboard attempt could not exercise live input because desktop access was unavailable and its viewer process was forcibly terminated. Its VM cleanup does not establish graceful window teardown. The resumed record still has `cleanupComplete=false`. A separate viewer exit-code file contains `0`, which does not establish final VM cleanup. Retirement did not manipulate any VM.

Open boundaries include ARM64, other Sandbox packages, cold broker activation, mixed-DPI monitor transfers, live unsupported-server display behavior and shell fallback, every product launch/close choice, physical and general shortcuts, staging reclamation, and uncertain-state recovery across process restarts. Initial headless hangs and name-scope lookup failures remain in local reports separately from the final passing results.

## Retained findings for future implementation

The experiment demonstrated x64 Native AOT Avalonia/ActiveX hosting and internal desktop attachment to one inspected Sandbox package. `poc/managed-ai-runtime` retains this research without the retired viewer implementation. The [evidence summary](rdp-sandbox-evidence.json) and original per-stage artifacts define the limits of those findings.

A future effort can assess public support contracts, per-package maintenance, ARM64 and DPI validation, and session/staging lifetime before reusing the design. Current development continues from the [existing Managed AI implementation](managed-ai-runtime.md). Reimplementation can use these observations as historical input while validating private contracts and uncertain outcomes again in its own environment.
