# Production skill execution smoke harness

[한국어](README.ko.md)

As of October 27, 2026, this harness reproduces the production DI, installed-skill registration, AppContainer script execution and actual Codex conversation checks. Its source does not declare a test passed or a release qualified. The [marketplace guide](../../docs/SKILL_MARKETPLACE.md) describes the supported implementation and service boundaries.

The harness separates direct script results, actual model/MCP events, instruction-compatible responses and synthetic host reports. It uses the existing TableCloth-owned Codex profile and preserves pre-existing skill enabled states. The sections below cover preparation, contained scripts, model requests, native product provenance and retained evidence.

## Production DI with the existing app-owned profile

`Program.cs` calls the real `UseTableCloth()` composition and retains `ISharedLocations`, runtime, skill, marketplace and chat services. The capturing process runner forwards all process calls and captures stdout only for actual `codex exec --json`; it does not capture authentication/status stdout or authentication material. It neither copies credentials nor changes global Codex settings, system PATH or the registry. The dedicated profile keeps `shell_tool=false` and `unified_exec=false`. See the [composition](../../src/TableCloth.App/DependencyInjection/ManagedAiWindowsServiceCollectionExtensions.cs).

The existing TableCloth runtime and login are prerequisites for actual chats. `--discover` invokes normal native skill discovery and product bridge preparation; preparation can install or verify the dedicated script runtimes. It is not a promise of no local runtime changes. BOK and KOSIS direct checks make bounded public-service requests; those services can fail independently of contained execution.

Current runs store the app-owned runtime, profiles, installed skills, worker files and diagnostic logs under `%LOCALAPPDATA%\TableCloth.Data\ManagedAi`. The Codex profile is `profiles\openai-codex\codex-home` beneath that data root. This location is outside the installer-owned `%LOCALAPPDATA%\TableCloth` directory; the product executable remains in `TableCloth\current`. Authentication material stays in the dedicated profile and is excluded from sidecars and collected evidence. Both live native fixture scripts reject seeds pointing at the former `TableCloth\ManagedAi` location, and the installed-package harness verifies the recorded current data root.

`--install-catalog` explicitly opts into the normal marketplace installation of missing active catalog skills. New installations persist after the run. Existing packages, source revisions and enabled states remain unchanged; a disabled or invalid required skill produces a recorded failure. Without this flag, the harness does not install missing catalog skills.

`--install-runtime <version>` explicitly installs the selected app-owned Codex runtime through the production installer before discovery, for example `--discover --install-runtime 0.156.1 --install-catalog`. It does not sign in, import authentication or change global Codex. Complete sign-in yourself in the dedicated profile before actual conversation checks. The option is omitted during installed-package verification so that those checks do not conceal a missing runtime by installing it first.

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
& ./tools/managed-skill-smoke/native-fixtures/python-concurrency/Invoke-PythonNetworkConcurrency.ps1 -HostExecutable '<probe-output>/TableCloth.exe' -SeedManifest '<probe-output>/evidence/<run>/runtime-seed-manifest.json' -EvidenceDirectory 'D:/Projects/TableCloth/Artifacts/python-concurrency-positive'
& ./tools/managed-skill-smoke/native-fixtures/python-concurrency/Invoke-PythonNetworkConcurrency.ps1 -HostExecutable '<probe-output>/TableCloth.exe' -SeedManifest '<probe-output>/evidence/<run>/runtime-seed-manifest.json' -EvidenceDirectory 'D:/Projects/TableCloth/Artifacts/python-concurrency-control' -LegacyControl
& ./tools/managed-skill-smoke/native-fixtures/Test-ServiceResultValidation.ps1
```

Replace placeholders with absolute local paths and use a separate evidence directory for each run. Repeat `--direct`, the 22-case mode and `--chat all` with `--nativepath` to tie those checks to the chosen product host. The independent script checks Python package imports, Node CommonJS/ESM modules, supported public GET routes, synthetic environment exclusion and denied file/direct-network access through actual `TableCloth.exe --skill-mcp`. It validates local paths and seed runtime hashes, and cleans its uniquely owned fixture directory unless `-KeepFixtures` is requested. Native host provenance and successful fixture results are both required for a NativeAOT execution claim.

The [Python concurrency fixture](native-fixtures/python-concurrency/Invoke-PythonNetworkConcurrency.ps1) checks correlated concurrent responses, URL/HTTP errors and timeout recovery inside the real contained Python worker. Its responder is synthetic and makes no upstream requests. The normal invocation expects the current transport to preserve those contracts. `-LegacyControl` installs the earlier racy transport inside the fixture and expects a contained worker failure with observed response cross-talk. The control script succeeds only when it reproduces that expected failure; it does not qualify the earlier transport. [Test-ServiceResultValidation.ps1](native-fixtures/Test-ServiceResultValidation.ps1) separately checks synthetic KOSIS responses, including HTTP 200 API errors, without contacting the service.

The [independent Win32 observer](ProcessEvidenceObserver.cs) runs outside the broker during live probe chats and native broker fixtures. It samples only descendant processes at the exact expected product and runtime paths, obtains image paths and creation times from Windows, hashes those images and queries `TokenIsAppContainer` from the actual process tokens. It retains failed queries as incomplete evidence. It does not read command lines, environment variables or authentication files. The native fixture saves these observations as `processes.json`; probe chats save per-skill process observations.

For an installed-package check, [Invoke-InstalledSkillProbe.ps1](Invoke-InstalledSkillProbe.ps1) accepts `-InstalledDirectory "$env:LOCALAPPDATA\TableCloth\current"`, `-ProbeDirectory '<built-probe-output>'`, `-EvidenceDirectory '<repository>/Artifacts/<new-run>'` and `-ExpectedProductSha256 '<independently-trusted-product-hash>'`. Install the matching NativeAOT package first and build the diagnostic parent separately. The script checks the installed executable's hash and absent CLR header, records every original installation file's path, size and hash, and temporarily adds diagnostic DLLs plus the probe's two JSON launch files. Top-level DLLs retain their names; DLLs under `runtimes/win`, `runtimes/win-x64`, `runtimes/win-arm64` and `runtimes/win-x86` retain their relative paths. Other platform assets are excluded. Existing files are never overwritten; a colliding dependency must already have identical bytes. It invokes the diagnostic DLL from that installation directory, so unchanged product DI selects the actual installed `TableCloth.exe`, then runs discovery, direct scripts, all 22 calculator cases and all six chats. The parent remains a managed diagnostic process; this check does not establish behavior of the NativeAOT GUI parent.

The script copies the raw evidence into the new workspace evidence directory, verifies the copy against its source, and runs the read-only verifier with the supplied product hash. It preserves process stdout, stderr, exit codes, original/final installation manifests and the cleanup assessment. Cleanup removes only the newly created sidecars whose hashes still match and the exclusively owned probe evidence directory after a matching archive exists. Newly created Windows runtime directories are tracked separately and removed in reverse order only when empty; pre-existing directories remain intact. Unexpected changes fail the run and remain visible in its summary. A pre-existing installation `evidence` directory is rejected to avoid assuming ownership of earlier results. GUI requests and separate process observations can qualify the installed GUI in an additional step; content-free application outcome counters alone do not prove an MCP call.

## Read-only verification with an independently trusted product hash

The [evidence verifier](EvidenceVerifier.cs) reconstructs the assessment from raw provider MCP events, runtime metadata and independent process observations. Obtain the product SHA-256 from a separately trusted build or release payload rather than from the bundle being checked. Supply that hash when reviewing a saved all-skill chat bundle.

```powershell
dotnet '<probe-output>/AllSkillsProbe.dll' --verify '<evidence-directory>' --expected-product-sha256 '<trusted-product-sha256>'
```

This mode only reads the saved evidence and prints a new verification report with exit code zero for acceptance or one for rejection. It does not start the product, request a model response, install runtimes or modify the bundle. The verifier matches MCP start/completion events and arguments, script results, process ancestry and observation times, exact product/runtime image paths and hashes, and successful kernel-token queries showing AppContainer workers. It does not accept saved summary flags, model claims or the broker's `wasAppContainer` field alone. Older bundles without independent OS observations fail the script-execution checks; a later offline check cannot recreate measurements that were never captured.

Omitting `--expected-product-sha256` leaves only internal bundle consistency and reports `ProductHashExternallyAnchored=false`. Supplying a trusted hash anchors the selected product identity but does not cryptographically attest evidence files produced by a malicious recorder. A missing or failed live observation remains a verification failure even if the worker actually ran. The report-writing, certificate and Sandbox response cases also remain response-only evidence; their drafts and synthetic reports do not prove real host actions.

Offline verification identifies the storage layout separately. New bundles report `RuntimeStorageLayout=update-safe-data-root`. Existing bundles from `TableCloth\ManagedAi` remain readable as `RuntimeStorageLayout=legacy-installation-root` with `HistoricalStorageLayout=true`; a passing historical bundle does not qualify the current storage configuration or installer data retention. Other application directory names and suffix lookalikes are rejected. Archived legacy seeds are never accepted by the live fixture scripts.

## Evidence files and qualification limits

Each probe invocation writes `bin/.../evidence/<UTC timestamp>-<suffix>/`. `probe.jsonl` records initial/final installed states and selected runtime/host metadata. Per-skill provider JSONL contains actual `exec --json` events and `<skill-id>.processes.json` retains the independent Win32 measurements; direct and chat JSON files keep outcomes and their assessment. `calculator-cases/` preserves each result, its aggregate and input dataset. `runtime-seed-manifest.json` records verified runtime coordinates for the independent broker fixture; its invocation snapshots may already be removed when the probe exits, so it is not a reusable skill registration.

The independent fixture script writes its request/response JSONL and summary to the explicit evidence directory. It distinguishes successful worker execution from successful live upstream data. No authentication or user certificate material belongs in retained evidence. Logs, binaries, build outputs and generated evidence are excluded from the tracked tool sources. Successful checks apply to the recorded host and inputs; they do not establish GUI behavior, every dependency, every host ACL or every Windows architecture.
