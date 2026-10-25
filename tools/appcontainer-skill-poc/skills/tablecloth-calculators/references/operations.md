# Calculator operations and conventions

This document specifies the original TableCloth calculator CLI as of October 25, 2026. The implementation uses Python 3.11 or later and only the standard library. It calculates from supplied values and fixed formulas; it does not retrieve rates, prices, exchange rates, credentials, or personal records.

The Python source, tests, operation names, validation rules, and examples were written for this contribution. They do not copy another calculator service's source, instructions, screen text, or layout. The primary references linked below explain mathematical concepts, standard-library behavior, and measurement conventions. They do not endorse this implementation or establish compatibility with a particular bank, tax authority, medical service, or commercial calculator.

## JSON transport and limits

Run `scripts/calculate.py` with one UTF-8 JSON object on standard input. There are no command-line options. The script reads at most 65,537 bytes to enforce a 65,536-byte input limit. It writes one JSON line to standard output and returns exit status 0 on success or 2 on a rejected request. The implementation does not evaluate expressions, invoke subprocesses, access files, or make network requests. For isolated execution, use Python's `-I -S -B` switches; this also disables site initialization and bytecode writes.

The following request applies one discount.

```json
{"operation":"discount.single","price":"100","rate_percent":"20"}
```

A successful response has this shape. Decimal results are strings, including money values. Counts, calendar-day differences, and period indexes are JSON integers.

```json
{"ok":true,"operation":"discount.single","result":{"original":"100.00","discount":"20.00","final":"80.00"},"assumptions":["Money uses 2 decimal places and ROUND_HALF_UP.","No tax or service-specific rules are applied.","Discounts multiply sequentially; only the final price is rounded. Discount = rounded original - rounded final."]}
```

A rejected request has this shape. Messages describe validation failures without returning the original payload or a traceback.

```json
{"ok":false,"error":{"code":"invalid_input","message":"A 100 percent discount cannot be reversed."}}
```

Decimal inputs may be JSON numbers or decimal strings. Strings are preferable when the sending application otherwise uses binary floating point. Scientific notation is accepted within the same bounds. The parser rejects duplicate object keys, NaN, Infinity, booleans, null, unknown operations, missing required fields, and fields not listed for the selected operation. It does not accept arbitrary expressions or permissive date/time strings.

- Maximum absolute numeric input: `1e15`.
- Maximum fractional precision: 12 places; at most 30 coefficient digits.
- Maximum absolute numeric result: `1e30`; decimal working precision: 50 digits.
- Money `places`: integer 0 through 6, default 2. Monetary input amounts round to these places before financial, price, discount, and split calculations.
- General numeric results: up to 12 fractional places, without trailing zeros. BMI uses exactly 2 places.
- Maximum contribution, compounding, or loan periods: 1,200. Loan schedules default to a summary; `schedule:"full"` returns at most 1,200 rows.
- Discount lists and split weights: 1 through 100 entries. Request objects have at most 20 fields, strings at most 128 characters, and arrays at most 1,200 elements.
- Maximum serialized response: 1 MiB. Out-of-range calculations fail rather than returning an infinity or a partial schedule.

`invalid_json` covers malformed UTF-8/JSON and multiple top-level values. `invalid_input` covers schema and explicit domain checks. `out_of_range` covers numeric/date arithmetic that cannot fit the supported range. Decimal rounding follows Python's documented [decimal arithmetic and ROUND_HALF_UP](https://docs.python.org/3/library/decimal.html#rounding-modes).

## Percentages, prices, and shared bills

Every request also contains `operation`. Required and optional fields are case-sensitive. Percentages are supplied as `20` for 20%, not `0.20`.

| Operation | Required fields | Optional fields | Result fields and convention |
| --- | --- | --- | --- |
| `percent.part` | `total`, `rate_percent` | None | `value = total * rate_percent / 100` |
| `percent.share` | `part`, `total` | None | `percent = part / total * 100`; total must not be zero; signed arithmetic |
| `percent.change` | `before`, `after` | None | `percent = (after - before) / before * 100`; before must be positive |
| `percent.adjust` | `value`, `rate_percent` | None | `value = input value * (1 + rate_percent / 100)`; a negative rate reduces the value |
| `percent.points` | `before_percent`, `after_percent` | None | `percentage_points = after_percent - before_percent`; no relative percentage calculation |
| `discount.single` | `price`, `rate_percent` | `places` | `original`, `discount`, `final`; rate 0 through 100 inclusive |
| `discount.sequential` | `price`, `rates_percent` array | `places` | Same result; multiply the remaining-price fractions in order, then round once |
| `discount.reverse` | `price`, `rate_percent` | `places` | Input price is the discounted price; rate must be less than 100; divide by the remaining-price fraction |
| `unit_price` | `total`, `quantity` | `per_quantity` (default 1), `places` | `unit_price = total / quantity * per_quantity`, plus `per_quantity`; quantities must be positive |
| `bill_split` | `total`, `weights` array | `places` | Rounded `total` and `shares` array; nonnegative weights with a positive sum |

Prices, bill totals, and financial money amounts are nonnegative. Money operations do not identify a currency, exchange currencies, add tax, or infer a service charge. All comparable amounts therefore use the same caller-specified unit of money.

Discounts derive the discount amount by subtracting the rounded final price from the rounded original price. This preserves `original = discount + final`, including half-minor-unit cases. Sequential discounts are not added: 20% followed by 10% leaves 72% of the original price. Commercial services may round at different stages; no service-specific convention is inferred.

Bill splitting uses exact rational proportions of the supplied decimal weights. It allocates whole minor units first, then distributes remaining units to the largest fractional remainders. Ties follow input order. Shares remain nonnegative and sum exactly to the rounded total. Equal splitting uses identical positive weights, for example `["1","1","1"]`.

## Fixed-rate financial formulas

Rates are supplied by the user. `annual_rate_percent` is a **nominal annual rate**, from 0 through 1,000 inclusive. Negative interest rates, effective-rate/APR conversions, automatic day-count conventions, taxes, fees, grace periods, variable-rate forecasts, lending eligibility, legal limits, and investment recommendations are outside this interface. These are fixed mathematical scenarios rather than quotes or product-specific repayment statements.

| Operation | Required fields | Optional fields | Results |
| --- | --- | --- | --- |
| `interest.simple` | `principal`, `annual_rate_percent`, `years` | `places` | `principal`, `interest`, `balance` |
| `interest.compound` | `principal`, `annual_rate_percent`, `years` | `compounds_per_year` (default 1), `places` | `principal`, `interest`, `balance` |
| `savings.recurring` | `initial`, `contribution`, `annual_rate_percent`, `periods`, `periods_per_year` | `timing` (`end` default or `begin`), `places` | `contribution`, `principal_total`, `interest`, `balance` |
| `savings.target` | `initial`, `target`, `annual_rate_percent`, `periods`, `periods_per_year` | `timing`, `places` | Same savings fields, plus `target` |
| `loan` | `principal`, `annual_rate_percent`, `periods`, `periods_per_year`, `method` | `schedule` (`summary` default or `full`), `places` | Repayment summary and optional schedule |

`years` is from 0 through 100. For compounding, `compounds_per_year` is an integer from 1 through 366, and `years * compounds_per_year` must be a whole number no greater than 1,200. A fractional number of compounding periods is rejected. For savings and loans, `periods` is an integer from 1 through 1,200 and `periods_per_year` is an integer from 1 through 366. Contributions and interest occur at the same frequency.

For annual rate `a = annual_rate_percent / 100`, principal `P`, and years `t`, simple interest is `P*a*t`. Compound balance is `P*(1+a/m)^(m*t)`, where `m` is the compounding frequency. The response rounds interest to the selected money precision and adds it to the rounded input principal. The [SEC Investor.gov compound-interest calculator](https://www.investor.gov/financial-tools-calculators/calculators/compound-interest-calculator) illustrates the separate roles of initial amount, contributions, time, annual rate, and compounding frequency; this CLI uses its own documented formulas and scope.

For recurring savings, let `r = a / periods_per_year`, `n = periods`, and `F = (1+r)^n`. The end-of-period contribution factor is `(F-1)/r`, or `n` when `r=0`. Beginning-of-period contributions multiply that factor by `1+r`. Balance is `initial*F + contribution*factor`. Principal total is `initial + contribution*n`; interest is the rounded balance minus that principal total. This is period-based compounding, without per-period interest rounding.

The target calculator solves `max(0, (target-initial*F)/factor)`. It rounds the required contribution **upward** to the selected minor unit, then recomputes the balance using that contribution. A contribution of zero means the projected initial balance already meets the supplied target. It does not imply the rate will be achieved in real use.

The following request calculates the amount to contribute at the end of each of three zero-interest periods.

```json
{"operation":"savings.target","initial":"0","target":"100","annual_rate_percent":"0","periods":3,"periods_per_year":12,"timing":"end"}
```

It returns contribution `33.34`, principal total/balance `100.02`, interest `0.00`, and target `100.00`.

### Loan methods and rounding reconciliation

`method` is one of `annuity`, `equal_principal`, or `bullet`. All methods charge the period's interest on its opening balance. The input principal must remain positive after rounding. The [CFPB explanation of amortization](https://www.consumerfinance.gov/ask-cfpb/what-is-amortization-and-how-could-it-affect-my-auto-loan-en-771/) explains the principal/interest split and amortization schedules; actual contracts can use different fees, dates, and rounding rules.

- `annuity`: theoretical payment `P*r/(1-(1+r)^(-n))`; use `P/n` when `r=0`.
- `equal_principal`: theoretical principal repayment `P/n`, plus interest on the remaining balance.
- `bullet`: interest-only payments followed by principal plus interest in the final period.

Regular amounts and period interest round with `ROUND_HALF_UP`. Principal repayments are capped at the remaining balance. The final period repays all remaining principal, so the last payment can differ from the regular payment. Tiny principals or very long terms can produce zero principal repayments in early periods after rounding; the last period still reconciles the balance. A rounded annuity payment below the period interest is rejected.

The summary contains `method`, `principal`, `periods`, `periodic_rate_percent`, `total_interest`, `total_payment`, and `final_balance`. Annuities also include `regular_payment`. Full schedules add `schedule`, an array of `period`, `opening_balance`, `interest`, `principal`, `payment`, and `closing_balance`. Every schedule preserves the sum of principal repayments, the sum of payments/interest, nonnegative balances, and a final balance of zero.

The following request exercises the final-payment adjustment.

```json
{"operation":"loan","principal":"1000","annual_rate_percent":"0","periods":3,"periods_per_year":12,"method":"annuity","schedule":"full"}
```

Its payments are `333.33`, `333.33`, and `333.34`, with total principal/payment `1000.00` and final balance `0.00`.

## Gregorian dates and fixed UTC offsets

Dates use exactly `YYYY-MM-DD`, in years 0001 through 9999. The engine uses the proleptic Gregorian calendar. Reference dates come from the request; it does not read the current clock. It does not calculate public holidays, lunar calendars, legal ages, or jurisdiction-specific working days. Python documents the Gregorian date model and fixed-offset distinction in [datetime](https://docs.python.org/3/library/datetime.html).

| Operation | Required fields | Optional fields | Result and convention |
| --- | --- | --- | --- |
| `date.diff` | `start`, `end` | `inclusion` (`elapsed` default or `both`) | Integer `days`; elapsed is signed end minus start; both includes both dates and requires end on or after start |
| `date.add` | `date` | Integer `years`, `months`, `days` (all default 0) | `date`; apply combined years/months once, clamp to the target month's last day, then add days |
| `date.age` | `birth_date`, `as_of` | `leap_day` (`feb28` default or `mar1`) | Completed integer `years`; birth must not be after as_of; February 29 convention applies in non-leap years |
| `date.weekday` | `date` | None | `iso_weekday` (Monday 1 through Sunday 7), English `weekday` |
| `time.convert` | `datetime`, `from_offset`, `to_offset` | None | ISO `source`, `converted`, and `utc` date/time strings |

`date.add` accepts years from -9,999 through 9,999, months from -119,988 through 119,988, and days from -3,652,058 through 3,652,058; the final date must still be representable. Adding one month to January 31, 2025 yields February 28. Adding two months **from the original January 31 anchor** yields March 31. Repeatedly adding one month to the previous result would instead yield March 28; use the original anchor for each recurrence. Years and months combine before clamping: February 29, 2024 plus one year and one month yields March 29, 2025.

Elapsed days between February 28 and March 1, 2024 are 2; including both dates yields 3. The same date yields elapsed 0 or inclusive 1. “One day after” and “the first day starting on” therefore use different conventions; choose explicitly.

The datetime input uses exactly `YYYY-MM-DDTHH:MM[:SS[.ffffff]]`, without an embedded offset. Hours are 00 through 23 and minutes/seconds are 00 through 59. Signed fixed offsets use `+HH:MM` or `-HH:MM`, with absolute offset less than 24 hours. Fractional-hour offsets such as `+05:45` and `-03:30` are supported. Zero offsets normalize to `+00:00`. No leap seconds or `24:00` normalization is accepted.

The following request crosses a year boundary.

```json
{"operation":"time.convert","datetime":"2026-12-31T22:30","from_offset":"+00:00","to_offset":"+09:00"}
```

Its converted result is `2027-01-01T07:30:00+09:00`. A fixed offset does not change with the date. City names, `Asia/Seoul`, `America/New_York`, abbreviations such as `EST`, and automatic DST interpretation are unsupported. Python's [zoneinfo documentation](https://docs.python.org/3/library/zoneinfo.html#data-sources) describes the separate IANA database dependency and ambiguous local-time handling; this CLI does not load that database or infer either occurrence of a repeated wall-clock time.

## Fixed unit conversion and BMI numbers

`unit.convert` requires `value`, `from_unit`, and `to_unit`. Its result contains `value`, `unit`, and `family`. Unit identifiers are case-sensitive and both units must belong to the same family.

| Family | Accepted identifiers | Base/convention |
| --- | --- | --- |
| Length | `mm`, `cm`, `m`, `km`, `in`, `ft`, `yd`, `mi` | Metres; international foot = 0.3048 m; mile = 1609.344 m |
| Mass | `mg`, `g`, `kg`, `oz`, `lb` | Kilograms; avoirdupois pound = 0.45359237 kg; ounce = 1/16 pound |
| Area | `sqm`, `sqft`, `pyeong` | Square metres; square foot = 0.09290304 sqm; pyeong = 400/121 sqm |
| Volume | `ml`, `l`, `m3` | Cubic metres; litre = 0.001 m3 |
| Speed | `m/s`, `km/h`, `mph` | Metres per second |
| Duration | `s`, `min`, `h`, `day`, `week` | Seconds; day = 86,400 seconds; week = 7 days |
| Temperature | `C`, `F`, `K` | Celsius, Fahrenheit, kelvin; affine conversion; values below absolute zero are rejected |

The fixed international/avoirdupois unit conventions follow the [NIST SI conversion guide](https://www.nist.gov/pml/special-publication-811/nist-guide-si-appendix-b-conversion-factors). Pyeong is an explicit calculator convention rather than an SI unit or a property-valuation rule. No currency exchange, survey-foot alternative, calendar month/year duration, cooking-unit convention, or context-dependent conversion is inferred. Signed linear values are allowed; temperature must remain at or above absolute zero.

`bmi` requires positive `weight_kg` and `height_cm`. It returns only `bmi`, calculated as `weight_kg / (height_cm/100)^2` and rounded to 2 decimal places. For example, 70 kg and 175 cm yields `22.86`. It accepts no age, sex, diagnostic thresholds, or growth-chart data. The [CDC BMI explanation](https://www.cdc.gov/bmi/about/index.html) supplies the formula and describes its interpretation limits; this operation does not produce a diagnosis, weight-status category, child percentile, or treatment advice.

## Verification and unsupported requests

From the skill directory, run `python -I -S -B -m unittest discover -s tests -v`. Set `PYTHONDONTWRITEBYTECODE=1` as well when invoking other Python tools. The tests cover ordinary outputs and numerical/date limits, schema errors, conservation of rounded totals, zero/tiny rates, schedule reconciliation, leap years, anchored month ends, inclusive dates, fractional offsets, date crossover, incompatible units, and BMI number-only output. They do not contact or validate live financial or medical services.

Tax, insurance, legal/regulatory limits, live market or foreign-exchange data, holiday/lunar databases, location-based time-zone selection, investment advice, financial-product comparisons, PRO simulators, and service-specific calculations have no operations in this version. An unsupported request returns an error; it is not redirected to a network service or an arbitrary Python expression.
