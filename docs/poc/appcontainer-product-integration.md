# AppContainer integration for skill scripts

[한국어](appcontainer-product-integration.ko.md)

The Preview 7 preparation branch connects the reviewed calculator to TableCloth chat through the current product executable and installs dedicated, pinned Python and Node.js workers automatically. A fresh Windows x64 installation on October 26, 2026, verified both runtime versions and read/write access to paths longer than 320 characters inside AppContainer.

This document covers the product call path, supported calculations, runtime integrity, marketplace diagnostics, skill lifecycle and recorded evidence. The sections describe the current behavior before separating historical experiments and outstanding qualification.

> These measurements describe Preview 7 preparation as of October 26, 2026. They do not establish Windows 10 22H2 or ARM64 execution support, final installer verification, signing or publication.

## Product calls through the current executable

The chat provider obtains enabled local skills and asks [WindowsManagedAiSkillExecution](../../src/TableCloth.ManagedAi.Windows/WindowsManagedAiSkillExecution.cs) for a bridge. An enabled, valid calculator with the reviewed script hash causes the bridge to prepare the dedicated runtimes and register its MCP tool. No manual activation receipt or copied product host participates.

The route is `TableCloth chat -> dedicated Codex CLI -> tablecloth_skills/tablecloth-calculators -> current TableCloth.exe --skill-mcp -> AppContainer Python worker`. The [product entry point](../../src/TableCloth/Program.cs) handles the broker before GUI, updater or application-log initialization. The app-owned profile retains `shell_tool=false`, `unified_exec=false`, the read-only Codex sandbox and approval policy `never`. The [invocation builder](../../src/TableCloth.ManagedAi.OpenAi/Execution/OpenAiCodexInvocationBuilder.cs) approves only the registered calculator for the current invocation. [Codex MCP configuration](https://learn.chatgpt.com/docs/extend/mcp) supplies transport; the [native runner](../../src/TableCloth.ManagedAi.AppContainer/Native/AppContainerProcessRunner.cs) verifies the AppContainer token before resuming the worker.

## One reviewed calculator and six operations

The adapter accepts `tablecloth-calculators/scripts/calculate.py` only when its SHA-256 equals `279b627d4350b8895717a6ac3597a6e87cab7a9882004d639251ebbee3e7c947`. A changed or missing script blocks execution. The registered operations are `percent.part`, `discount.single`, `loan`, `date.add`, `time.convert` and `bmi`. See the [adapter](../../src/TableCloth.ManagedAi.Windows/WindowsManagedAiSkillExecution.cs).

The worker receives validated JSON and runs a fixed Python entry point without network capability. Model arguments cannot supply shell commands, executable paths or script source. The [broker](../../src/TableCloth.ManagedAi.AppContainer/Broker/SkillExecutionRegistry.cs) provides no pip, npm or npx workflow. Node.js is installed and exercised by native runtime probes; no Node.js marketplace adapter, BOK adapter or KOSIS adapter is registered. Installing a runtime does not make every catalog script executable.

## Dedicated runtimes with archive and dependency verification

[WindowsManagedAiScriptRuntimeManager](../../src/TableCloth.ManagedAi.Windows/ScriptRuntimeManager.cs) selects Windows x64 or ARM64 packages for Python 3.14.9 embeddable and Node.js 24.22.1. [ScriptRuntimePackages](../../src/TableCloth.ManagedAi.Windows/ScriptRuntimePackages.cs) pins the official URLs and SHA-256 values using [Python release metadata](https://www.python.org/ftp/python/3.14.9/windows-3.14.9.json) and [Node checksums](https://nodejs.org/dist/v24.22.1/SHASUMS256.txt).

The first supported calculator invocation installs or repairs runtimes under the app-owned `ManagedAi/skill-execution` directory. Installation rejects archive hash mismatches and unsafe entries, stages extraction and compares every installed file with its pinned archive entry. Node includes `node.exe` and `LICENSE`; npm and npx are excluded. Global PATH, system runtimes, the registry and global Codex settings remain unchanged.

Each invocation creates a unique host-owned manifest with the reviewed registration and `runtimeFiles` hashes. The [broker](../../src/TableCloth.ManagedAi.AppContainer/Broker/SkillExecutionRegistry.cs) keeps verified dependencies and the script open with read-only sharing through worker execution. Before repair, the installer asks the native runner to recover pending ACL journals under its cross-process lease. Recovery failure or a journal that remains after obtaining the installation lease blocks repair. Installation receipts record observations and do not replace pinned-archive verification.

## Marketplace versions and isolated long-path checks

The third marketplace tab, **Runtimes**, displays installed Python and Node.js versions and separate long-path results. **Install or repair runtimes** prepares the dedicated packages. **Check installation and environment** validates existing packages and reruns the probes without downloading missing packages. See the [marketplace UI](../../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml).

Each [probe](../../src/TableCloth.ManagedAi.Windows/ScriptRuntimeManager.cs) starts the dedicated interpreter through AppContainer with networking disabled, creates a temporary path longer than 320 characters and writes and reads a file there. Results are `true` for success, `false` for a measured path failure and `null` when the result cannot be established. The UI warns about possible feature limitations for either restricted or unverified results. It hides that warning only when both runtimes pass and discards earlier success when a new check fails.

The results cover the current interpreter and isolated temporary directory. Additional libraries can impose other limits. Product installation and broker paths retain their own short-path checks. The probe does not change Windows long-path policy. See the [runtime UI state](../../src/TableCloth.App/ViewModels/SkillMarketplaceWindowViewModel.Runtimes.cs).

## Installation, updates and removal through the marketplace

The [marketplace](../../src/TableCloth.App/Dialogs/SkillMarketplaceWindow.axaml) provides the following workflow.

1. Install and enable `tablecloth-calculators` from the catalog.
2. Open **Runtimes**, inspect the versions and path results, and use **Install or repair runtimes** to prepare missing packages before chatting.
3. Start a new chat and request a supported calculation. Use a successful calculator MCP result to establish script execution; model attribution text alone does not establish it.
4. Update, disable or remove the skill from **Installed skills**. The next invocation registers no bridge for a disabled or removed skill.

A changed script hash requires an updated reviewed adapter. A previous staged copy cannot replace a missing or changed installed script. Disabling a skill retains shared runtimes. [SkillExecutionBridgeTests](../../src/TableCloth.ManagedAi.Test/SkillExecutionBridgeTests.cs) cover these conditions.

The earlier `Enable-ProductSkillExecution.ps1` procedure belongs to the historical manual-activation experiment. Preview 7 preparation uses the current executable and automatic runtime preparation and does not read that activation receipt.

## Current runtime evidence and historical model experiments

The tracked [production-runtime and provider evidence](appcontainer-production-runtime-evidence.json) summarizes `Artifacts/runtime-preview7-check/evidence.json`, its installation receipt and `Artifacts/release-v1.22.0-preview.7/product-routing`. Fresh x64 installation verified Python 3.14.9 and Node.js 24.22.1 inside AppContainer, with successful read/write checks beyond 320 characters for both. The runtime implementation's 19 focused unit tests passed.

The final local provider check resolved the actual `OpenAiCodexProvider.ChatAsync` through dependency injection and used the app-owned Codex CLI 0.156.1, dedicated profile and real GPT-6-Luna model. Its completed MCP call executed `discount.single` with price 1,000,000 and rate 10. The worker recorded `wasAppContainer=true`, exit code `0`, discount `100000.00` and final price `900000.00`; the provider returned the same amounts. The JSONL contains one completed MCP call, no completed command-execution calls and a completed turn. `shell_tool=false`, `unified_exec=false`, the read-only Codex sandbox and approval policy `never` remained in effect. The tracked summary retains output-file hashes and excludes authentication data, thread IDs and absolute user paths.

The final local Release checks passed 219 Managed AI tests, 334 TableCloth tests and 43 Spork tests. The [tracked evidence](appcontainer-production-runtime-evidence.json) records these suites separately from focused runtime tests and historical PoC counts. This provider check uses the current framework-dependent product executable; signed release artifacts and automated GUI interaction require their own verification.

The earlier [product-connection evidence](appcontainer-product-integration-evidence.json) records the manual-activation experiment on October 25, 2026. Three invocation-builder sessions and one `OpenAiCodexProvider.ChatAsync` session used GPT-6-Luna and recorded completed calculator MCP calls, `wasAppContainer=true` and exit code `0`. They produced a KRW 100,000 discount and KRW 900,000 final price from KRW 1,000,000, and BMI 27.36. Source and log hashes describe that historical working tree and do not qualify the final Preview 7 installer or product package.

The [standalone PoC report](../../tools/appcontainer-skill-poc/RESULTS.md), including deterministic model-stub routing, describes another historical measurement layer. Earlier harness and Managed AI totals are not combined with current product checks. Native execution, real model routing, UI verification and released-artifact verification establish different facts.

## Remaining platform and security qualification

Package mappings and official checksums exist for x64 and ARM64. The current fresh-install evidence covers actual x64 execution on the development machine. Windows 10 22H2, ARM64 execution, automated GUI interaction and forced power-loss recovery remain outside that evidence. Build artifacts, signing and publication follow the [release process](../RELEASING.md).

Ordinary AppContainer shares the host kernel and can read resources whose existing ACLs permit its token. Token verification, restricted ACL grants, network denial and Job Objects do not establish that every host path is inaccessible. Microsoft explains the boundary in its [AppContainer implementation guidance](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer).

The product now has an automatic path for the reviewed calculator and dedicated runtime diagnostics. Broader marketplace scripts, network-enabled adapters and additional target platforms retain separate implementation and evidence requirements.
