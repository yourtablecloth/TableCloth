# AppContainer skill execution proof of concept

[한국어](README.ko.md)

This directory preserves the standalone evaluation of fixed Python and Node.js scripts in ordinary Windows AppContainer processes. Its commands and measurements describe the original PoC; product chat retains `shell_tool=false` and uses the shared native runner through its reviewed adapter.

Preview 7 preparation connects one reviewed Python calculator through the current `TableCloth.exe --skill-mcp`, automatically prepares pinned x64 or ARM64 Python and Node.js packages, and restores runtime versions and isolated long-path diagnostics in the marketplace. It requires no manual activation receipt or copied host. See [product integration and evidence](../../docs/poc/appcontainer-product-integration.md) for supported operations, runtime integrity and qualification limits. Node.js has no product marketplace adapter. The standalone measurements below remain historical PoC results.

The experiment covers a native worker, a fixed-script MCP broker, pinned skill fixtures and an evaluation-only catalog. It separates offline broker checks, actual contained execution, service access and Codex routing.

The following sections describe the execution boundary, reproducible commands, skill adaptations, measured results and remaining product work.

## Host broker and contained workers

The trusted host exposes registered skill IDs and JSON input through a small stdio MCP server. It never accepts executable paths, shell commands or script text from tool arguments. Registered executable and script hashes are checked before each call. Python starts with `-I -S -B -X utf8`; Node runs a fixed `.cjs` file with `--preserve-symlinks-main`. Reparse paths remain rejected. Neither path calls `cmd`, PowerShell, pip, npm or npx.

The native runner creates a unique AppContainer profile, grants that SID read and execute access to staged runtime/script directories, and grants write access to per-call work/home/temp directories. It creates the process suspended, verifies `TokenIsAppContainer`, assigns a Job Object, and then resumes it. The default worker has no network capability. See Microsoft's [AppContainer implementation](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer) and [Job Object documentation](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects).

Ordinary AppContainer shares the host kernel. Windows resources whose ACLs already grant `ALL APPLICATION PACKAGES` can remain readable. Synthetic private-file denial does not establish that every host path is inaccessible. Job Objects bound resources and lifetime; AppContainer tokens and ACLs enforce the access boundary. No Windows Sandbox, VM, firewall changes, global PATH changes or global Codex changes participate.

## Dedicated preparation and evaluation commands

The PoC uses the repository's .NET 11 SDK and Windows x64. The current evaluation machine runs build 26300. Windows 10 22H2 and ARM64 remain separate qualification targets. The native APIs predate Windows 10, but API availability does not establish runtime and product compatibility on those targets. See [CreateAppContainerProfile requirements](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-createappcontainerprofile).

The commands below build the host, stage verified archives and run only contained workers. `prepare` downloads missing fixed archives into the repository's artifact cache and rejects a hash mismatch. It writes only under `Artifacts`. The optional public-API command also reruns the local checks.

```powershell
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet build tools/appcontainer-skill-poc/Poc.csproj -c Release
$poc = 'tools/appcontainer-skill-poc/bin/Release/net11.0-windows10.0.18362.0/TableCloth.AppContainerPoc.dll'
dotnet $poc prepare $PWD.Path
$ownedRoot = Join-Path $PWD.Path 'Artifacts/appcontainer-skill-poc'
dotnet $poc selftest $ownedRoot
dotnet $poc evaluate-public-api $ownedRoot
dotnet $poc codex-probe $ownedRoot
```

Preparation pins [Python 3.14.9 embeddable x64](https://www.python.org/downloads/release/python-3149/) to SHA-256 `3c01d7cfc666f746a6f064f60fb2156ab0ec464d3d738ab796371a46a9bb24a2` and [Node.js 24.22.1 Windows x64](https://nodejs.org/dist/v24.22.1/) to `d2046d28c2c3dcd211ac5145c0bf30e2047ef9dfb306693c8c6e2eccd8b95fa5`. These are experiment inputs, not an automatic upgrade policy. The official archives retain their own license notices.

## Three reactivated skills and explicit adapters

The evaluation-only catalog reactivates `tablecloth-calculators`, `bok-ecos-stats` and `kosis-stats`. It also retains the three previously eligible instruction skills. The parent catalog PR and production catalog remain separate. Source commits and all fixture files are recorded in [skills/ORIGINS.json](skills/ORIGINS.json); the original files retain their licenses. See the [fixture guide](skills/README.md) for command and credential contracts.

The calculator runs its exact pinned Python JSON-stdin script. Six operation families enter the broker allowlist: a percentage part, a single discount, loan repayment, date addition, fixed UTC-offset conversion and numeric BMI. No tax rates, medical classification or live financial data are supplied.

BOK and KOSIS upstream instructions use an npm-based instruction loader. This experiment invokes their pinned standalone Python helpers through explicit adapters. It does not reproduce or qualify the upstream `npx @nomadamas/k-skill@0` workflow. The Node fixture proves contained Node execution independently; it is not an additional marketplace skill.

The broker blocks every registration marked `requiresNetwork`. KOSIS's broker registration uses `--dry-run`. The separate host-owned public-API evaluation grants `internetClient` for one fixed BOK public-sample request and one fixed KOSIS shared-proxy request. That capability is broader than a domain allowlist. It does not authorize network-enabled marketplace tools. No user API keys, auth files or host credential environment variables enter workers.

## Evidence from actual execution

The harness writes `Artifacts/appcontainer-skill-poc/evaluation.json` and, when requested, `evaluation-public-api.json`. Each record contains the check name, outcome and bounded execution evidence. Offline checks inject a fake executor and retain the `broker-offline` prefix. They establish validation behavior only.

Actual native cases cover calculation results, Node JSON execution, KOSIS dry-run, AppContainer token verification, private canary read/write denial, permitted work writes, network access-denied responses, child-process rejection, secret-environment omission, memory/output/time limits, cancellation, ACL restoration and real stdio MCP dispatch. A network timeout does not count as enforced network denial.

The final run passed 46/46 harness checks, including 20 fake-executor broker checks. It also ran 28 upstream calculator tests and 19 BOK tests inside AppContainer. The existing Managed AI suite passed 197/197. These counts describe different test layers and are not combined into a native-security test count.

`codex-probe` passed actual Codex 0.156.1 -> MCP -> AppContainer -> pinned calculator routing with shell tools disabled. A loopback deterministic Responses stub supplied the tool call; no real model inference or authentication occurred. Its fresh profile explicitly approves only the registered calculator tool, keeps the server default at `prompt`, and retains global approval policy `never`. The production profile remains unchanged.

The [evaluation report](RESULTS.md) records the final measurements and failures. Fixed upstream code and citations remain in the [fixture guide](skills/README.md). No fabricated service response substitutes for a failed live request.

## Product integration and remaining qualification

The original standalone prototype was not connected to the TableCloth chat window or its installer. The [current product adapter](../../docs/poc/appcontainer-product-integration.md) automatically installs dedicated pinned runtimes, verifies all installed dependencies against the official archives, records `runtimeFiles` hashes and keeps verified files locked during execution. The current product executable hosts the broker; the earlier `Enable-ProductSkillExecution.ps1` activation procedure is historical. A remote marketplace XML entry cannot add a script registration. Additional adapters, network policies, forced power-loss recovery and target-OS execution qualification remain separate work.

The PoC journals original DACLs and mandatory labels before grants, serializes ACL changes across processes with an owned-file lease, and blocks execution while a recovery journal remains. `recover-acl <owned-root>` replays owned-file identities and restores those descriptors before clearing a journal. Recovery was exercised during diagnosis; forced power-loss recovery has not been qualified. Restoration compares DACLs and actual mandatory-label ACEs because Windows can retain empty-SACL control flags after removing a Low label.

Codex's [MCP configuration](https://learn.chatgpt.com/docs/extend/mcp) supplies the tool transport. MCP itself does not isolate code. The fixed CLI's ability to route a registered tool with shell tools disabled is reported separately from real ChatGPT inference, authentication and the production application's strict profile policy.

In this experiment, AppContainer provides a lightweight process boundary for registered scripts. A successful local worker demonstrates a feasible execution path within the measured limits.

Production marketplace eligibility continues to follow its existing branch policy. Network-enabled skills and general npm workflows remain separate work until their policy and qualification evidence cover the broader access.
