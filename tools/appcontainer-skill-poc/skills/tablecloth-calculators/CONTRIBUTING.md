# Contributing independent calculator implementations

[한국어](CONTRIBUTING.ko.md) | [README](README.md) | [Copyright policy](docs/COPYRIGHT.md)

This project accepts independently written, standard-library-only calculations over explicit user inputs. Maintainers review each contribution's scope, provenance, licensing and tests before inclusion.

## Scope without external services or datasets

Describe the public calculation method, required inputs, units, assumptions and rounding. Extend the [operation reference](references/operations.md) with the actual JSON contract and examples. Support Python 3.11 or later without downloads, network requests, API keys, environment-provided credentials or pip dependencies.

Tax and regulatory calculations, statutory benefits, current rates or prices, holiday and lunar calendars, named-zone DST datasets, medical classifications, diagnosis and treatment are outside scope. Basic BMI can calculate the numerical index without importing comparison populations or clinical categories. Do not reproduce site-specific PRO workflows, analyses or reports.

## Provenance and rights for each contribution

Include the following in the pull request:

- Original implementation authorship and any source material consulted
- Links to primary public formula or specification sources, with the relevant version or date
- The license and usage terms of every proposed non-original item, or an explicit statement that no non-original material is included
- Independent derivation of the implementation, assumptions, rounding and test cases
- Test commands and results for the changed behavior

Public visibility and a familiar formula do not grant permission to copy another implementation. Do not copy, translate, paraphrase or rewrite third-party source code, interface layouts, help text, reports, examples or datasets into a contribution. Cite public mathematical methods and write the implementation, interface and explanation independently. Do not claim a service owns a general method merely because it uses a PRO or branded label. The [copyright policy](docs/COPYRIGHT.md) describes the boundary without promising legal clearance.

Maintainers can request further evidence or decline material whose origin or rights remain unresolved. Attribution alone does not make otherwise excluded material acceptable. Do not submit actual credentials, private financial records or personal health information in code, fixtures, documentation, issues or pull requests.

## Validation before review

Add behavior tests for representative results, invalid inputs, domain limits and rounding where relevant. Use synthetic values and independently derived expectations. Tests should not scrape or compare against a third-party calculator service.

Run the standard-library tests from the repository root:

```text
python -m unittest discover -s tests -v
```

The [workflow](.github/workflows/test.yml) covers Python 3.11 and 3.14 on Linux and Windows. Keep [SKILL.md](SKILL.md), the operation reference and both README languages consistent with the implemented behavior.

## Existing license notices

Preserve [LICENSE-AGPL](LICENSE-AGPL), [LICENSE-COMMERCIAL](LICENSE-COMMERCIAL) and existing attribution. Discuss any licensing exception with maintainers before inclusion. A pull request does not create a separate commercial agreement or override third-party rights.
