# AppContainer integration for skill scripts

[한국어](appcontainer-product-integration.ko.md)

**For current development behavior, read the [skill marketplace and contained execution guide](../SKILL_MARKETPLACE.md).** This page preserves the earlier Preview 7 preparation record; its calculator-only adapter and measurements do not describe the current generic registration path.

As of October 27, 2026, the development implementation discovers packaged Python and Node.js entry points in enabled, valid installed skills and registers them with the application-owned MCP broker. The active catalog has three script skills (BOK, KOSIS and calculators), one instruction-only report skill and two existing host-report skills (certificate expiry and Windows Sandbox). Instruction-only and host-report skills keep their own contracts rather than gaining a script worker. See the [current routing and limits](../SKILL_MARKETPLACE.md#mcp-routing-and-the-appcontainer-boundary).

Workers retain no network capability. Eligible BOK and KOSIS requests use the bounded public HTTPS GET host transport; other destinations, user credentials and arbitrary package-manager workflows remain unavailable. The dedicated profile retains `shell_tool=false` and `unified_exec=false`; public pinned instructions replace the upstream `npx` loader. This summary describes implementation paths, not a new test result or release qualification. The [current guide](../SKILL_MARKETPLACE.md) distinguishes those paths from execution evidence and service availability.

## Historical scope of the Preview 7 preparation record

The Preview 7 preparation branch connected the reviewed calculator to TableCloth chat through the product executable and installed dedicated, pinned Python and Node.js workers automatically. A fresh Windows x64 installation on October 26, 2026, verified both runtime versions and read/write access to paths longer than 320 characters inside AppContainer.

The following sections retain that historical call path, six-operation calculator limit, runtime diagnostics, lifecycle and measured evidence. They do not impose the old reviewed-calculator hash requirement on the later generic registration implementation.

> All measurements and calculator-only behavior below describe Preview 7 preparation as of October 26, 2026. They do not establish later generic script support, Windows 10 22H2 or ARM64 execution support, final installer verification, signing or publication. Current implementation details belong to the [marketplace guide](../SKILL_MARKETPLACE.md).

## Historical product calls through the then-current executable

During Preview 7 preparation, the chat provider obtained enabled local skills and asked `WindowsManagedAiSkillExecution` for a bridge. An enabled, valid calculator with the reviewed script hash caused the bridge to prepare the dedicated runtimes and register its MCP tool. No manual activation receipt or copied product host participated. The [current source](../../src/TableCloth.ManagedAi.Windows/WindowsManagedAiSkillExecution.cs) has since replaced that calculator-only registration with generic installed entry-point discovery.

The route is `TableCloth chat -> dedicated Codex CLI -> tablecloth_skills/tablecloth-calculators -> current TableCloth.exe --skill-mcp -> AppContainer Python worker`. The [product entry point](../../src/TableCloth/Program.cs) handles the broker before GUI, updater or application-log initialization. The app-owned profile retains `shell_tool=false`, `unified_exec=false`, the read-only Codex sandbox and approval policy `never`. The [invocation builder](../../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiCodexInvocationBuilder.cs) approves only the registered calculator for the current invocation. [Codex MCP configuration](https://learn.chatgpt.com/docs/extend/mcp) supplies transport; the [native runner](../../src/TableCloth.ManagedAi.AppContainer/Native/AppContainerProcessRunner.cs) verifies the AppContainer token before resuming the worker.

## Historical reviewed calculator and six-operation restriction

The Preview 7 adapter accepted `tablecloth-calculators/scripts/calculate.py` only when its SHA-256 equaled `279b627d4350b8895717a6ac3597a6e87cab7a9882004d639251ebbee3e7c947`. A changed or missing script blocked execution. Its registered operations were `percent.part`, `discount.single`, `loan`, `date.add`, `time.convert` and `bmi`. These restrictions describe the historical adapter, not the [current implementation](../../src/TableCloth.ManagedAi.Windows/WindowsManagedAiSkillExecution.cs).

At that stage, the worker received validated JSON and ran a fixed Python entry point without network capability. Model arguments could not supply shell commands, executable paths or script source. The broker provided no pip, npm or npx workflow. Node.js was installed and exercised by native runtime probes; no Node.js marketplace adapter, BOK adapter or KOSIS adapter was registered. This calculator measurement did not establish support for other catalog scripts.

## Historical runtime archive and dependency verification

[WindowsManagedAiScriptRuntimeManager](../../src/TableCloth.ManagedAi.Windows/ScriptRuntimeManager.cs) selects Windows x64 or ARM64 packages for Python 3.14.9 embeddable and Node.js 24.22.1. [ScriptRuntimePackages](../../src/TableCloth.ManagedAi.Windows/ScriptRuntimePackages.cs) pins the official URLs and SHA-256 values using [Python release metadata](https://www.python.org/ftp/python/3.14.9/windows-3.14.9.json) and [Node checksums](https://nodejs.org/dist/v24.22.1/SHASUMS256.txt).

The first supported calculator invocation installs or repairs runtimes under the app-owned `ManagedAi/skill-execution` directory. Installation rejects archive hash mismatches and unsafe entries, stages extraction and compares every installed file with its pinned archive entry. Node includes `node.exe` and `LICENSE`; npm and npx are excluded. Global PATH, system runtimes, the registry and global Codex settings remain unchanged.

Each invocation creates a unique host-owned manifest with the reviewed registration and `runtimeFiles` hashes. The [broker](../../src/TableCloth.ManagedAi.AppContainer/Broker/SkillExecutionRegistry.cs) keeps verified dependencies and the script open with read-only sharing through worker execution. Before repair, the installer asks the native runner to recover pending ACL journals under its cross-process lease. Recovery failure or a journal that remains after obtaining the installation lease blocks repair. Installation receipts record observations and do not replace pinned-archive verification.

## Historical marketplace versions and isolated long-path checks

The third marketplace tab, **Runtimes**, displays installed Python and Node.js versions and separate long-path results. **Install or repair runtimes** prepares the dedicated packages. **Check installation and environment** validates existing packages and reruns the probes without downloading missing packages. See the [marketplace UI](../../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml).

Each [probe](../../src/TableCloth.ManagedAi.Windows/ScriptRuntimeManager.cs) starts the dedicated interpreter through AppContainer with networking disabled, creates a temporary path longer than 320 characters and writes and reads a file there. Results are `true` for success, `false` for a measured path failure and `null` when the result cannot be established. The UI warns about possible feature limitations for either restricted or unverified results. It hides that warning only when both runtimes pass and discards earlier success when a new check fails.

The results cover the current interpreter and isolated temporary directory. Additional libraries can impose other limits. Product installation and broker paths retain their own short-path checks. The probe does not change Windows long-path policy. See the [runtime UI state](../../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.Runtimes.cs).

## Historical calculator installation, updates and removal

The [marketplace](../../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml) provides the following workflow.

1. Install and enable `tablecloth-calculators` from the catalog.
2. Open **Runtimes**, inspect the versions and path results, and use **Install or repair runtimes** to prepare missing packages before chatting.
3. Start a new chat and request a supported calculation. Use a successful calculator MCP result to establish script execution; model attribution text alone does not establish it.
4. Update, disable or remove the skill from **Installed skills**. The next invocation registers no bridge for a disabled or removed skill.

The historical adapter required an updated reviewed adapter after a script hash change. It did not substitute a previous staged copy for a missing or changed installed script. Disabling a skill retained shared runtimes. The later generic implementation snapshots the currently installed package and records fresh hashes; see the [current lifecycle](../SKILL_MARKETPLACE.md#local-installation-updates-and-enabled-state) and [registration tests](../../src/TableCloth.ManagedAi.Test/WindowsSkillRegistrationTests.cs).

The earlier `Enable-ProductSkillExecution.ps1` procedure belongs to the historical manual-activation experiment. Preview 7 preparation uses the current executable and automatic runtime preparation and does not read that activation receipt.

## Recorded Preview 7 runtime evidence and earlier model experiments

The tracked [production-runtime and provider evidence](appcontainer-production-runtime-evidence.json) summarizes `Artifacts/runtime-preview7-check/evidence.json`, its installation receipt and `Artifacts/release-v1.22.0-preview.7/product-routing`. Fresh x64 installation verified Python 3.14.9 and Node.js 24.22.1 inside AppContainer, with successful read/write checks beyond 320 characters for both. The runtime implementation's 19 focused unit tests passed.

The final local provider check resolved the actual `OpenAiCodexProvider.ChatAsync` through dependency injection and used the app-owned Codex CLI 0.156.1, dedicated profile and real GPT-6-Luna model. Its completed MCP call executed `discount.single` with price 1,000,000 and rate 10. The worker recorded `wasAppContainer=true`, exit code `0`, discount `100000.00` and final price `900000.00`; the provider returned the same amounts. The JSONL contains one completed MCP call, no completed command-execution calls and a completed turn. `shell_tool=false`, `unified_exec=false`, the read-only Codex sandbox and approval policy `never` remained in effect. The tracked summary retains output-file hashes and excludes authentication data, thread IDs and absolute user paths.

The final local Release checks for that preparation stage passed 219 Managed AI tests, 334 TableCloth tests and 43 Spork tests. The [tracked evidence](appcontainer-production-runtime-evidence.json) records these suites separately from focused runtime tests and earlier PoC counts. That provider check used the then-current framework-dependent product executable; it did not verify signed release artifacts or automated GUI interaction.

The earlier [product-connection evidence](appcontainer-product-integration-evidence.json) records the manual-activation experiment on October 25, 2026. Three invocation-builder sessions and one `OpenAiCodexProvider.ChatAsync` session used GPT-6-Luna and recorded completed calculator MCP calls, `wasAppContainer=true` and exit code `0`. They produced a KRW 100,000 discount and KRW 900,000 final price from KRW 1,000,000, and BMI 27.36. Source and log hashes describe that historical working tree and do not qualify the final Preview 7 installer or product package.

The [standalone PoC report](../../tools/appcontainer-skill-poc/RESULTS.md), including deterministic model-stub routing, describes another historical measurement layer. Earlier harness and Managed AI totals are not combined with current product checks. Native execution, real model routing, UI verification and released-artifact verification establish different facts.

## Limits of the historical platform and security qualification

Package mappings and official checksums existed for x64 and ARM64. The recorded Preview 7 fresh-install evidence covers actual x64 execution on the development machine. Windows 10 22H2, ARM64 execution, automated GUI interaction and forced power-loss recovery remain outside that evidence. Build artifacts, signing and publication follow the [release process](../RELEASING.md).

Ordinary AppContainer shares the host kernel and can read resources whose existing ACLs permit its token. Token verification, restricted ACL grants, network denial and Job Objects do not establish that every host path is inaccessible. Microsoft explains the boundary in its [AppContainer implementation guidance](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer).

Preview 7 preparation established the recorded calculator path and dedicated runtime diagnostics. The later generic Python/Node.js registration and bounded public GET transport have their own implementation and evidence scope in the [current marketplace guide](../SKILL_MARKETPLACE.md); the calculator-only historical measurements above do not qualify those later paths or additional platforms.
