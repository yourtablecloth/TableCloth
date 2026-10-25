# TableCloth financial and everyday calculators

[한국어](README.ko.md) | [Copyright and independent implementation](docs/COPYRIGHT.md) | [Contributing](CONTRIBUTING.md)

`tablecloth-calculators` provides a Codex skill and a local Python calculator for financial and everyday arithmetic. Its Korean display name is **금융 및 생활 계산기**. The calculator uses supplied values without web requests, external datasets, API keys or third-party Python packages.

Public calculation methods guide the implementation. Third-party source code, interfaces, explanatory text, reports and datasets are outside this package's reuse scope. The [copyright policy](docs/COPYRIGHT.md) explains the distinction and contribution requirements.

## Supported calculations and explicit inputs

The maintained [operation reference](references/operations.md) lists supported operations, JSON fields, units, assumptions and rounding. Calculations cover financial and everyday quantities rather than personalized financial recommendations. Basic BMI returns an arithmetic index without health classifications or population comparisons.

This package excludes taxes, statutory benefits, regulatory eligibility, live rates and prices, holiday or lunar calendars, named-zone daylight-saving datasets, medical interpretation and site-specific PRO analysis or reports. A PRO label alone does not establish exclusive legal rights in a public calculation method.

## Installation from the repository root

Import this folder through TableCloth's installed-skills view, or install it from the skill marketplace after a catalog lists this repository. The root [SKILL.md](SKILL.md) makes the catalog path `Source.Path="."`; a catalog entry also pins a full commit. Installing this skill does not install Python or change global settings.

The command-line calculator requires Python 3.11 or later. With TableCloth, install its dedicated Python runtime and use the absolute path supplied in `KSKILL_PYTHON`. Outside TableCloth, select an absolute interpreter path explicitly. No pip dependencies or additional service credentials are required.

## Calculation requests and execution boundaries

Ask Codex for a supported calculation, such as a percentage or discount. The [skill instructions](SKILL.md) route the request to [scripts/calculate.py](scripts/calculate.py), using one JSON request on stdin. In an execution environment where TableCloth supplies `KSKILL_PYTHON`, replace the script path in this PowerShell example with the installed skill's absolute path:

```powershell
$request = '{"operation":"discount.single","price":"100","rate_percent":"20"}'
$request | & $env:KSKILL_PYTHON -I -S -B 'C:\absolute\skill\scripts\calculate.py'
```

In a separate terminal without that variable, use the chosen interpreter's absolute path in place of `$env:KSKILL_PYTHON`. The flags `-I -S -B` isolate Python imports, skip site initialization and suppress bytecode writes. This request returns `ok: true` with `result.final` equal to `"80.00"`. Decimal strings avoid unintended input rounding. The [operation reference](references/operations.md) defines the full contract. Invalid input returns `ok: false` and exit code 2. A failed execution produces a limitation or input correction, not a guessed result.

The calculator CLI makes no network requests. A Codex chat can still require an online account and transmit conversation content to its provider. Shell permission enables a broader execution surface than this script: the skill's instructions do not enforce a Python-only security boundary. TableCloth's permissions remain applicable.

## Contributions and local validation

Contributions follow [CONTRIBUTING.md](CONTRIBUTING.md), including provenance, licensing, independent implementation and maintainer review. Do not submit copied or translated site content, rewritten third-party code, PRO clones or extracted data tables.

Run the standard-library test suite from the repository root with Python 3.11 or later:

```text
python -m unittest discover -s tests -v
```

The [CI workflow](.github/workflows/test.yml) runs this command on Linux and Windows with Python 3.11 and 3.14. It does not publish packages or deploy services.

## Original project licensing

This package includes TableCloth's [AGPL license text](LICENSE-AGPL) and [commercial-license contact notice](LICENSE-COMMERCIAL) unchanged. The original notice states:

> Copyright 2021-2026 rkttu.com. All rights reserved.

The commercial notice names Jung Hyun, Nam as the contact. A contribution's provenance and license information supports maintainer review; it does not replace the licenses or grant rights in third-party material. See the [copyright policy](docs/COPYRIGHT.md).
