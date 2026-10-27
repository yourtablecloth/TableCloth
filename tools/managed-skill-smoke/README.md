# Production skill execution smoke harness

[한국어](README.ko.md)

As of October 27, 2026, this harness reproduces the production DI, installed-skill registration, AppContainer script execution and actual Codex conversation checks. Its source does not declare a test passed or a release qualified. The [marketplace guide](../../docs/SKILL_MARKETPLACE.md) describes the supported implementation and service boundaries.

The harness separates direct script results, actual model/MCP events, instruction-compatible responses and synthetic host reports. It uses the existing TableCloth-owned Codex profile and preserves pre-existing skill enabled states. The sections below cover preparation, contained scripts, model requests, native product provenance and retained evidence.

## Production DI with the existing app-owned profile

`Program.cs` calls the real `UseTableCloth()` composition and retains `ISharedLocations`, runtime, skill, marketplace and chat services. The observer forwards all process calls and captures stdout only for actual `codex exec --json`; it does not capture authentication/status stdout or authentication material. It neither copies credentials nor changes global Codex settings, system PATH or the registry. The dedicated profile keeps `shell_tool=false` and `unified_exec=false`. See the [composition](../../src/TableCloth.App/DependencyInjection/ManagedAiWindowsServiceCollectionExtensions.cs).

The existing TableCloth runtime and login are prerequisites for actual chats. `--discover` invokes normal native skill discovery and product bridge preparation; preparation can install or verify the dedicated script runtimes. It is not a promise of no local runtime changes. BOK and KOSIS direct checks make bounded public-service requests; those services can fail independently of contained execution.

`--install-catalog` explicitly opts into the normal marketplace installation of missing active catalog skills. New installations persist after the run. Existing packages, source revisions and enabled states remain unchanged; a disabled or invalid required skill produces a recorded failure. Without this flag, the harness does not install missing catalog skills.

## Serial build and product bridge discovery

Build from the repository root using the SDK selected by `global.json`, serially with other solution builds. This example builds the managed diagnostic parent and discovers actual installed registrations without a model request.

```powershell
dotnet build tools/managed-skill-smoke/AllSkillsProbe.csproj -c Release
dotnet tools/managed-skill-smoke/bin/Release/net11.0-windows10.0.18362.0/AllSkillsProbe.dll --discover
```

The project references the application library for DI and the real product host with `ReferenceOutputAssembly=false`. Its after-build target obtains that host's actual output directory through `GetTargetPath` and copies exactly `TableCloth.exe`, `TableCloth.dll`, `TableCloth.deps.json` and `TableCloth.runtimeconfig.json` beside the probe. The production `ISharedLocations` resolver selects that executable for MCP children. A successful managed parent build does not prove that this child is NativeAOT. Discovery records the actual host path, hash, version and MCP tool list.

## Direct scripts and all 22 calculator operations

`--direct` calls the registered BOK, KOSIS and calculator implementations through the actual product registry. `--node-fixture` separately tests a temporary JSON-stdin Node.js package and removes its original package in `finally`; that fixture is not a marketplace skill. This example additionally runs all pinned calculator cases while the same bridge remains alive.

```powershell
$calculatorDataset = (Resolve-Path tools/managed-skill-smoke/calculator-cases.json).Path
dotnet tools/managed-skill-smoke/bin/Release/net11.0-windows10.0.18362.0/AllSkillsProbe.dll --direct --node-fixture --calculator-cases $calculatorDataset
```

Each calculator case requires contained success, exit code zero, a successful script envelope, nonempty assumptions and `JsonElement.DeepEquals(result, expectedResult)`. The dataset covers the [22 pinned operations](https://github.com/yourtablecloth/tablecloth-calculators/blob/877b58cf0741db21342445f2b3fcb54ad1ada85c/references/operations.md), including monetary reconciliation and date boundaries. It is expected input/output data, not a stored successful run. Actual outcomes and the exact dataset bytes/hash are saved separately. Failed cases do not suppress the remaining calculator cases.

## Actual Codex conversations and the natural BMI request

`--chat all` calls the production chat provider once for each active catalog skill using the existing app-owned login. `--chat <skill-id>` selects one case. The default model is `gpt-6-luna`; this example states that choice explicitly.

```powershell
dotnet tools/managed-skill-smoke/bin/Release/net11.0-windows10.0.18362.0/AllSkillsProbe.dll --chat all --model gpt-6-luna
```

The calculator case sends the natural request `키 171cm, 몸무게 80kg인 사람의 BMI는 얼마인가요? tablecloth-calculators 스킬을 활용해서 계산해주세요.` It supplies no operation name or Python field schema in that request. Success requires the real completed MCP event, contained worker result and BMI `27.36`; the model's skill attribution alone is insufficient.

The report-writing case checks an instruction-compatible draft without forcing script execution. Certificate-expiry and Windows Sandbox cases attach clearly labeled synthetic `LocalCertificateReport` and `LocalWindowsSandboxReport` data. They do not scan real certificates, list real Sandbox sessions or change a session. These response checks do not prove that the model read a particular instruction file. Public pinned-context metadata and script execution events retain separate evidence scopes. See the [chat request contract](../../src/TableCloth.ManagedAi.Core/Contracts.cs).

## Native product host and independent broker fixtures

After building the managed probe, copy the chosen actual NativeAOT product payload beside `AllSkillsProbe.dll`, preserving the managed dependencies needed by the diagnostic parent. Use a payload whose build/commit provenance you can record. `--nativepath` validates equality with the real production `ISharedLocations` result; it does not replace DI or copy binaries. Run discovery first to obtain a current verified runtime seed, then point the independent broker fixture script at that exact executable and saved seed.

```powershell
dotnet '<probe-output>/AllSkillsProbe.dll' --discover --nativepath '<probe-output>/TableCloth.exe'
& ./tools/managed-skill-smoke/native-fixtures/Invoke-NativeSkillSmoke.ps1 -HostExecutable '<probe-output>/TableCloth.exe' -SeedManifest '<probe-output>/evidence/<run>/runtime-seed-manifest.json' -EvidenceDirectory 'D:/Projects/TableCloth/Artifacts/managed-skill-smoke-native'
```

Replace placeholders with absolute local paths and use a separate evidence directory for each run. Repeat `--direct`, the 22-case mode and `--chat all` with `--nativepath` to tie those checks to the chosen product host. The independent script checks Python package imports, Node CommonJS/ESM modules, supported public GET routes, synthetic environment exclusion and denied file/direct-network access through actual `TableCloth.exe --skill-mcp`. It validates local paths and seed runtime hashes, and cleans its uniquely owned fixture directory unless `-KeepFixtures` is requested. Native host provenance and successful fixture results are both required for a NativeAOT execution claim.

## Evidence files and qualification limits

Each probe invocation writes `bin/.../evidence/<UTC timestamp>-<suffix>/`. `probe.jsonl` records initial/final installed states and selected runtime/host metadata. Per-skill provider JSONL contains actual `exec --json` events; direct and chat JSON files keep outcomes and their assessment. `calculator-cases/` preserves each result, its aggregate and input dataset. `runtime-seed-manifest.json` records verified runtime coordinates for the independent broker fixture; its invocation snapshots may already be removed when the probe exits, so it is not a reusable skill registration.

The independent fixture script writes its request/response JSONL and summary to the explicit evidence directory. It distinguishes successful worker execution from successful live upstream data. No authentication or user certificate material belongs in retained evidence. Logs, binaries, build outputs and generated evidence are excluded from the tracked tool sources. Successful checks apply to the recorded host and inputs; they do not establish GUI behavior, every dependency, every host ACL or every Windows architecture.
