# Pinned skill evaluation fixtures

[한국어](README.ko.md)

As of October 25, 2026, these files support the `poc/appcontainer-skill-evaluation` experiment. They do not establish script support in production TableCloth chat. The upstream files remain byte-identical to the public commits recorded in [ORIGINS.json](ORIGINS.json).

This guide covers provenance, command contracts, network dependencies and the unverified execution boundary. It separates a proposed direct-Python adapter from the upstream CLI workflow.

The source records come first, followed by command inputs, service access, credentials, licenses and result reporting.

## Fixed upstream sources

The BOK and KOSIS fixtures come from [NomaDamas/k-skill at 0102e5133865f00886e37ac8cf39a49e2c7a3d39](https://github.com/NomaDamas/k-skill/tree/0102e5133865f00886e37ac8cf39a49e2c7a3d39). The calculator comes from [877b58cf0741db21342445f2b3fcb54ad1ada85c](https://github.com/yourtablecloth/tablecloth-calculators/tree/877b58cf0741db21342445f2b3fcb54ad1ada85c). The origin manifest records each source path, byte count and Git blob SHA-1. No upstream file is adapted in place.

## Python command contracts

Both NomaDamas helpers use Python 3.9+ and the standard library; the calculator requires Python 3.11+. They import no sibling helper module. BOK and KOSIS accept command-line arguments, while the calculator reads one JSON object from stdin. Their full contracts remain in [BOK instruction.md](bok-ecos-stats/instruction.md), [KOSIS instruction.md](kosis-stats/instruction.md) and the calculator's [operations reference](tablecloth-calculators/references/operations.md).

The following argument forms describe proposed adapter inputs. They are not unrestricted execution instructions or evidence that a service call succeeded.

```text
<private-python> -I -S -B -X utf8 <BOK>/scripts/bok_ecos.py key --limit 1 --secrets-path <owned-empty-file>
<private-python> -I -S -B -X utf8 <BOK>/scripts/bok_ecos.py search --alias 기준금리 --start 20260101 --end 20260102 --secrets-path <owned-empty-file>
<private-python> -I -S -B -X utf8 <KOSIS>/scripts/run_kosis_stats.py search --query 인구 --result-count 1 --json --dry-run
<private-python> -I -S -B -X utf8 <calculator>/scripts/calculate.py
stdin: {"operation":"discount.single","price":"100","rate_percent":"20"}
```

## Loader adaptation and package boundaries

The original [BOK SKILL.md](bok-ecos-stats/SKILL.md) and [KOSIS SKILL.md](kosis-stats/SKILL.md) require `npx -y @nomadamas/k-skill@0 instruct ...` and describe helper execution through that CLI. Reading the pinned `instruction.md` and invoking the pinned standalone Python file can avoid that loader in this experiment. This changes the execution path and does not reproduce the CLI's runtime-specific instruction assembly. The experiment retains the original instructions and safety rules. The moving npm `@0` selector is not the fixed Git commit and does not authorize arbitrary package downloads, lifecycle scripts or updates.

## Network and credential limits

The [BOK helper](bok-ecos-stats/scripts/bok_ecos.py) directly calls `https://ecos.bok.or.kr/api/` and falls back to the public `sample` key, capped at ten rows per call. It has no dry-run option. It resolves a user key before falling back, including a default `~/.config/k-skill/secrets.env` read attempt. An evaluation therefore uses an isolated owned home, a cleared credential environment and an explicit owned empty `--secrets-path`.

The [KOSIS helper](kosis-stats/scripts/run_kosis_stats.py) sends ordinary lookups to `https://k-skill-proxy.nomadamas.org/v1/kosis/...`; that service supplies its own KOSIS key. Its `--dry-run` only renders the URL and parameters and performs no HTTP call. Proxy configuration, reachability and upstream response remain external dependencies. `--direct` and `bigdata` require user credentials and remain outside this experiment. The [calculator](tablecloth-calculators/scripts/calculate.py) uses no network or API key. No real user key is read, requested or supplied by this fixture preparation.

## Upstream licenses

The BOK and KOSIS copies retain the exact same-commit MIT license in [bok-ecos-stats/LICENSE](bok-ecos-stats/LICENSE) and [kosis-stats/LICENSE](kosis-stats/LICENSE). Their original copyright notices continue to apply. The calculator retains [LICENSE-AGPL](tablecloth-calculators/LICENSE-AGPL), [LICENSE-COMMERCIAL](tablecloth-calculators/LICENSE-COMMERCIAL) and its [copyright policy](tablecloth-calculators/docs/COPYRIGHT.md). Copying these files into the TableCloth repository does not replace those upstream licenses.

## Result reporting boundary

Fixture preparation verified all 31 files against the public pinned Git blobs without running them. The subsequent [evaluation](../RESULTS.md) passed 46 harness checks, including contained execution and fixed public API requests. A real Codex/MCP/AppContainer calculator call passed using a deterministic local Responses stub, with real model inference and authentication each at zero. Installation, model discovery, contained execution, real inference and production integration retain separate result categories.

Production chat keeps its existing execution policy. A blocked or unavailable service remains a failed or unverified live case, and fabricated data never substitutes for it.
