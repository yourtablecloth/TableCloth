# AppContainer skill execution evaluation results

[한국어](RESULTS.ko.md)

On October 25, 2026, the Windows x64 experiment passed all 46 harness checks and an actual Codex 0.156.1 MCP routing probe. The host reported Windows build 26300. Production TableCloth chat and its shell policy remain unchanged.

The results cover contained Python and Node execution, pinned skill adapters, fixed public API requests, access and resource limits, and tool routing driven by a local deterministic stub. Real model inference and production integration remain untested.

This report identifies the measured environment, test layers, corrected compatibility issues, skill outcomes and remaining qualification. [evaluation-summary.json](evaluation-summary.json) retains sanitized evidence and hashes of the local raw reports.

## Fixed environment and evidence sources

The experiment used Python 3.14.9 embeddable x64, Node.js 24.22.1 x64, Codex 0.156.1 and SDK 11.0.100-rc.1.26425.128. Runtime archive hashes and reproduction commands appear in the [PoC guide](README.md); upstream file identities and licenses appear in [ORIGINS.json](skills/ORIGINS.json).

Raw local evidence resides in `Artifacts/appcontainer-skill-poc/evaluation-public-api.json`, `codex-probe-evidence.json` and `test-results/appcontainer-poc-managed-ai.trx`. The committed summary excludes machine-specific paths, profile SIDs, credentials and full service response bodies. Successful AppContainer API use on build 26300 does not qualify Windows 10 22H2 or ARM64. Microsoft's [profile API requirements](https://learn.microsoft.com/en-us/windows/win32/api/userenv/nf-userenv-createappcontainerprofile) describe availability separately.

## Distinct verification layers

The following counts retain their original scope. Harness totals include the two contained upstream test runs as two checks; their individual unit tests are not added to the harness total.

| Layer | Result | Established scope |
| --- | --- | --- |
| Offline broker checks | 20/20 | Fake executor, input/path/hash/protocol validation only |
| Other harness checks | 26/26 | Real workers, policy rejection, controls, public requests and cleanup |
| Contained calculator tests | 28/28 | Exact upstream arithmetic and input validation suite |
| Contained BOK tests | 19/19 | Exact upstream helper unit suite, no live requests in that suite |
| Existing Managed AI regression | 197/197 | Catalog parser, installation and existing backend behavior |
| Real CLI routing probe | Passed | Codex -> MCP -> AppContainer -> calculator, local response stub |

Python and Node denied private canary reads/writes while permitting work-directory writes. Neither inherited a synthetic host secret. Network-disabled Python returned Winsock 10013; Node returned `EACCES`. Python child creation returned Win32 1816. Node returned `UNKNOWN/-4094` under a one-process Job and successfully created the same fixed child under a two-process Job control. The broker always retains a one-process limit.

Timeout, cancellation, bounded output and a 64 MiB memory allocation probe passed. DACLs and actual mandatory-label ACEs matched before and after execution. Native per-run workspaces and ACL journals were empty after the final probes. These checks exercise the implemented boundary and do not establish a general sandbox escape audit. See Microsoft's [AppContainer implementation](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer).

## Compatibility fixes exercised by the final run

The runner restores changed DACLs and mandatory labels separately. Removing a temporary Low label uses a non-null empty ACL when no original label existed. Windows can retain empty-SACL control flags, so restoration compares the DACL and mandatory ACE bytes instead of assuming identical SDDL text. An owned-file lease serializes processes, and a pending ACL journal blocks new launches until recovery succeeds. Actual journal recovery was exercised; forced termination and power-loss recovery remain unqualified.

Node starts its fixed main file with `--preserve-symlinks-main` to avoid canonicalization that attempted to inspect the drive root. Reparse and hard-link checks still apply. Additional third-party modules and npm loaders are outside the measured scope. See the [Node CLI option](https://nodejs.org/docs/latest-v24.x/api/cli.html#--preserve-symlinks-main).

Python starts with `-I -S -B -X utf8`. `-I` ignores `PYTHONUTF8`, so the explicit UTF-8 option prevents CP949 output from being decoded as UTF-8 by the host. A Korean stdin/stdout round trip and the final BOK/KOSIS data checks passed. See [Python command-line isolation and UTF-8 options](https://docs.python.org/3.14/using/cmdline.html).

## Three skill outcomes and the Codex transport

`tablecloth-calculators` executed the exact pinned script through the broker. Discount, percentage, leap-day addition, UTC-offset date rollover, numeric BMI and zero-rate loan cases passed. BOK returned one public sample row through its exact Python helper. KOSIS returned one population-search row through its shared proxy. Their original npm instruction loaders were not executed; the two helper calls use explicit PoC adapters described in the [fixture guide](skills/README.md).

The generic broker blocks network-required registrations. BOK and KOSIS live tests use only fixed host-owned requests with `internetClient`, which grants broader access than a domain allowlist. The successful requests do not qualify arbitrary network-enabled marketplace tools. The calculator's successful Codex path uses a network-disabled worker.

The routing probe used a fresh `CODEX_HOME` and a loopback Responses stub. Codex advertised the calculator, received a namespaced tool call, executed the real broker and received `wasAppContainer=true`, `status=completed` and the calculated final amount `80.00`. Shell tools were absent. Only the exact calculator MCP tool has an `approve` override; global approval remains `never` and the server default remains `prompt`. No auth file was copied or created, no authorization header reached the stub, and real model inference occurred zero times. See the [official MCP configuration](https://learn.chatgpt.com/docs/extend/mcp).

## Evaluation catalog and product qualification boundary

The catalog experiment reactivates three script entries and retains three existing entries, for six active entries and nine commented candidates. [The catalog report](../../external/TableClothCatalog/docs/APP_CONTAINER_POC.md) documents the evaluation exception. API-key and unconnected MCP candidates remain deferred. The parent PRs and production catalog policy do not change through this draft experiment.

Product work still includes a reviewed execution-registration contract, complete dependency integrity, packaged broker/runtime lifecycle, consent and update behavior, production-profile integration, domain-limited network access, crash qualification and Windows 10/ARM64 tests. Ordinary AppContainer retains access to resources already granted to `ALL APPLICATION PACKAGES`; a VM boundary is not claimed.

The measured implementation supports lightweight contained execution for fixed Python and Node scripts while preserving disabled generic shell tools.

The calculator has the strongest tested tool path in this experiment. The two statistics helpers additionally demonstrate fixed public-service access. Production adoption remains a separate decision from these local PoC results.
