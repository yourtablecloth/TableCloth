# Codex CLI in Windows Sandbox research archive

On October 24, 2026, this archive records the `poc/codex-sandbox-computer-use` investigation for the `develop` documentation. The historical records describe Windows Sandbox runs with Codex CLI `0.156.1` and `0.164.0`. Both reached local CLI readiness and passed a disposable Service Control Manager (SCM) probe. Computer Use was not provisioned or demonstrated.

The archive preserves runtime staging, guest isolation, version-specific observations, the plugin-parser correction, and the remaining Computer Use qualification boundary. [한국어 문서](codex-sandbox-computer-use.ko.md) provides the Korean companion, and the [sanitized evidence summary](codex-sandbox-computer-use-evidence.json) retains outcomes and provenance hashes.

The sections below separate original observations from this documentation review, explain the prototype's boundaries, and preserve the conditions for further investigation. The [managed-AI implementation report](managed-ai-runtime.md) covers the application's separate host-side integration.

The following date notice limits the historical results and public-documentation snapshot.

> The recorded experiments took place on October 23 and October 24, 2026. This archive reviewed their local README files, scripts, and `validation.json` on October 24, 2026. It did not rerun Sandbox, Codex, authentication, model requests, or service probes. Official documentation was checked on that date; later runtime and product behavior may differ.

## 1. Local research provenance and evidence limits

The source branch pointed to commit `bf973738d423e69df39de0534d791a228677c6f0`. Its Git tree did not contain `tools/codex-sandbox-poc`; the eight local research files were untracked. The [evidence summary](codex-sandbox-computer-use-evidence.json) identifies that distinction and records the original files' SHA-256 hashes. The branch commit identifies the surrounding repository baseline, not a committed copy of the experiments.

The original `validation.json` records automated outcomes and separately labels user reports. This archive checked those records and read the scripts. It did not independently reproduce the original guest evidence or authenticate the package archives. A recorded hash identifies bytes for comparison; it does not establish origin or reproduce an observation by itself.

The preserved observations have three different sources:

- Automated historical guest bootstrap and CLI/SCM results
- Historical user reports of TUI entry, login, and the authenticated `0.156.1` plugin catalog
- Separate historical host and synthetic-fixture checks

## 2. Staged inputs and a disposable guest profile

`Prepare-CodexSandbox.ps1` accepted an explicit TableCloth-managed runtime directory and a new output directory. It checked the receipt, runtime layout, local paths, file bounds, and reparse points; copied the package and guest scripts; and wrote an input hash manifest and `Sandbox.wsb`. Preparation did not execute the runtime on the host. The generated `.wsb` file launched Windows Sandbox directly, consistent with [Microsoft's configuration-file workflow](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file).

The configuration mapped only staged `input` as read-only and `evidence` as writable. The guest checked mounted-file hashes, copied the runtime to a guest-local directory, verified the copy again, and created its own `CODEX_HOME` and work directory. It did not mount the host Codex profile, authentication files, TableCloth `Data`, or NPKI directories. Guest configuration disabled persisted conversation history and telemetry exporters. The scripts cleared selected API-key and session environment variables in their child process context.

Networking was enabled. `ProtectedClient`, vGPU, clipboard, microphone, camera, and printer redirection were disabled. `ProtectedClient = Disable` records this prototype's setting; it does not establish a Computer Use requirement or a production configuration. Networking and writable mapped evidence still cross isolation boundaries. Microsoft documents both the [network exposure and persistence of mapped-folder writes](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file#mapped-folders).

The guest shell exposed manual `Login-Codex`, `Start-Codex`, plugin-list, and marketplace-list helpers. Login used `codex login --device-auth`; the bootstrap neither authenticated nor invoked a model. Closing Sandbox discarded its local profile and services while already written mapped evidence remained on the host.

## 3. Two historical guest runs and their separate outcomes

The [original validation summary as condensed here](codex-sandbox-computer-use-evidence.json) records x64 guests reporting Windows version `10.0.26100.0`. Both compared 49 staged and copied file records with zero mismatches, reached `ReadyForManualLogin` at phase `Complete`, and returned exit code `0` for `--version`, `--help`, and `plugin --help`. The expected CLI version matched in each run.

| Observation | October 23, 2026: CLI `0.156.1` | October 24, 2026: CLI `0.164.0` |
| --- | --- | --- |
| Bootstrap and local CLI checks | Passed | Passed |
| Plugin discovery | Exit `0`; JSON parsed; `pluginCount: null` under the earlier parser | Exit `0`; schema recognized; installed `0`, available `0` |
| Disposable SCM probe | Compiled, registered, started in Session 0, stopped, and deleted | Same recorded outcome |
| Interactive CLI entry | User reported the welcome or login screen | Not user-confirmed |
| Guest login | User reported success after bootstrap | Not verified; bootstrap remained unauthenticated |
| Authenticated Computer Use catalog entry | User reported absence from `/plugins` | No authenticated catalog observation |
| Model request or Computer Use UI action | Not tested | Not tested |

The generic service probe compiled a small .NET Framework service and checked its Session 0 startup marker. It recorded stop and deletion outcomes. It did not install an OpenAI service, capture a desktop, or inject input. Service registration permission therefore does not establish interactive desktop access.

The bootstrap's `authenticated: false` describes the collection stage. The later `0.156.1` user report does not replace that field and does not carry authentication into the new `0.164.0` guest. Neither an empty unauthenticated catalog nor one account's authenticated catalog establishes global Computer Use availability.

## 4. Parser correction and separate host observations

The first collector parsed JSON but did not recognize the observed `installed`/`available` response shape. Its `pluginCount: null` remains unknown. The original raw guest response was not retained, so the later host response cannot reconstruct it. OpenAI's [CLI reference](https://learn.chatgpt.com/docs/cli/reference#codex-plugin) now documents these two arrays.

The corrected parser retained only fixed parsing metadata and counts. Its `pluginCount` follows the available-array count rather than summing installed and available entries. The real `0.164.0` guest exercised the correction and recorded zero in both arrays. The historical Windows PowerShell `5.1.26100.9711` fixture run reported 64 passing checks for staging, XML configuration, path handling, hashes, and parser regression cases. Those fixtures did not launch Sandbox or register a host service, and this archive did not rerun them.

Separate host checks used fresh unauthenticated profiles. The `0.156.1` check returned empty installed and available arrays without creating an authentication file. The `0.164.0` package probe reused the repository installer and recorded checksum, bounded extraction, layout, version, and receipt checks. It used a workspace profile adapter and did not qualify `WindowsManagedAiProfile` ACL handling. Its standard app-server schema exposed 202 methods and its experimental schema 272; the inspection identified no Computer Use installation or provisioning method. A `computer_use` feature value of `stable`/`true` did not demonstrate runtime readiness.

## 5. Computer Use provisioning and qualification still open

On the archive review date, [official OpenAI Computer Use documentation](https://learn.chatgpt.com/docs/computer-use) described setup through the ChatGPT desktop app and its Computer Use plugin. The inspected standalone CLI help and schemas did not establish a supported guest provisioning procedure. General CLI plugin management, a feature flag, configuration types, or a working SCM probe supplied no evidence of installed Computer Use tools.

The original investigation also noted that the bundled Computer Use instructions referred to `node_repl` and `@oai/sky`. Copying those instructions alone would not supply their runtime dependencies. The prototype did not install the ChatGPT desktop app in the guest, provision a Computer Use runtime/service, or demonstrate a tool session, screenshot, or UI action. It established no standalone-CLI Computer Use support claim.

Further qualification can use a documented provisioning route, verified guest tool availability, and a demonstrated guest UI action as acceptance criteria. Authentication, account catalog access, model inference, desktop access, and ARM64 execution each remain separate observations. The recorded x64 runs do not settle those other cases.

## 6. Preserved documentation and local-only originals

The maintained repository archive consists of this document, its [Korean companion](codex-sandbox-computer-use.ko.md), and the [evidence JSON](codex-sandbox-computer-use-evidence.json). The local research originals are retained separately under ignored `Artifacts/codex-sandbox-research/tools/codex-sandbox-poc/`. They are unavailable in a fresh checkout. This archive therefore provides no runnable reproduction command that assumes those scripts ship with the repository. The evidence JSON records their names and hashes, including the earlier live-run collector hashes that differ from the later parser revision.

The publication review retained fixed technical outcomes and hashes while omitting local account names, home paths, run/session identifiers, authentication material, raw plugin responses, conversation content, and screenshots. The original bootstrap intentionally exported bounded status and count metadata rather than raw CLI responses or exception text. This review covers the published archive; it does not certify every file in the ignored artifact directories.

This archive preserves the CLI isolation findings for the [TableCloth AI implementation history](managed-ai-runtime.md). It adds no runtime feature or release qualification. The documentation review validated JSON syntax, local links, and the correspondence between the summary and the retained research records.

## Research conclusion and continuation criteria

The investigation established historical evidence for standalone CLI startup and disposable SCM service operation in two x64 Sandbox guests. User reports additionally support TUI entry and login in the earlier `0.156.1` guest. The preserved records do not demonstrate Computer Use.

Future Computer Use work depends on supported provisioning and an observed guest tool session. Work that only needs the existing host-side TableCloth AI integration can refer to its separate implementation report. A new guest login, inference result, or UI automation result can extend this archive only with its own dated evidence.
