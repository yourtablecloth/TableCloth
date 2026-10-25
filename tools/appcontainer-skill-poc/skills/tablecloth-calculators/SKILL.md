---
name: tablecloth-calculators
description: Calculate financial and everyday quantities from user-supplied values, including percentages, discounts, interest, repayments, dates, fixed-offset time differences, unit conversions and basic BMI arithmetic. Use when the user requests a supported calculation. Excludes tax or regulatory advice, current market data, holiday or lunar calendars, named-zone DST rules and medical interpretation.
license: See LICENSE-AGPL and LICENSE-COMMERCIAL.
metadata:
  short-description: Financial and everyday calculations without external data
---

# Financial and everyday calculators

Korean display name: 금융 및 생활 계산기.

Read [references/operations.md](references/operations.md) for supported operations, exact input fields, units, rounding and examples. Select the operation that matches the request; ask for missing inputs or assumptions instead of inventing them.

Run the existing [scripts/calculate.py](scripts/calculate.py) with Python 3.11 or later and the flags `-I -S -B`. In TableCloth, use the absolute interpreter path supplied in `KSKILL_PYTHON`. Otherwise use an explicitly configured absolute Python interpreter path. Pass the absolute script path and send one JSON request through stdin. Do not substitute `python` from PATH when TableCloth supplies a dedicated interpreter. `-B` suppresses bytecode writes even when isolated mode ignores Python environment variables.

Use only this packaged calculator script. Do not generate or evaluate Python expressions, use `eval`, execute `python -c`, install packages, invoke unrelated shell commands, read unrelated files, browse the web or request API keys. The calculator uses only the Python standard library and user-supplied values.

Explain the returned result with its units, assumptions and rounding. If execution is unavailable, exits unsuccessfully or returns an error, report that limitation or obtain corrected inputs. Do not guess a result or present an unexecuted calculation as verified.

Keep basic BMI output to the numerical index. Do not infer health categories, diagnoses, treatment, reference percentiles or pregnancy and pediatric results. Use fixed UTC offsets only for time conversion; do not infer a named zone's daylight-saving rules. Reject unsupported tax, statutory benefit, regulatory eligibility, current-price and holiday or lunar-calendar requests.

The skill adds instructions, not a Python-only sandbox. TableCloth's execution permissions still apply. Follow the [copyright and contribution boundaries](docs/COPYRIGHT.md) when modifying the package.
