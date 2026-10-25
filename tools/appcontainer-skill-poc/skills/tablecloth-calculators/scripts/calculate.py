#!/usr/bin/env python3
"""Deterministic, standard-library calculators. Read one JSON object from stdin."""

from __future__ import annotations

import calendar
import datetime as dt
from decimal import Decimal, DecimalException, ROUND_CEILING, ROUND_HALF_UP, localcontext
from fractions import Fraction
import json
import re
import sys

MAX_INPUT_BYTES = 65_536
MAX_OUTPUT_BYTES = 1_048_576
MAX_PERIODS = 1_200
MAX_INPUT = Decimal("1e15")
MAX_RESULT = Decimal("1e30")
NUMBER = re.compile(r"[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?", re.ASCII)
DATE = re.compile(r"\d{4}-\d{2}-\d{2}", re.ASCII)
DATETIME = re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,6})?)?", re.ASCII)
OFFSET = re.compile(r"([+-])([01]\d|2[0-3]):([0-5]\d)", re.ASCII)


class CalculationError(ValueError):
    """A bounded, user-readable validation error."""


def require(condition: bool, message: str) -> None:
    if not condition:
        raise CalculationError(message)


def number(value, name: str) -> Decimal:
    require(not isinstance(value, bool), f"{name} must be a number, not a boolean.")
    require(isinstance(value, (str, int, Decimal)), f"{name} must be a decimal number.")
    text = str(value)
    require(len(text) <= 80 and NUMBER.fullmatch(text) is not None, f"{name} must be a finite decimal number.")
    result = Decimal(text)
    require(result.is_finite(), f"{name} must be finite.")
    require(-12 <= result.as_tuple().exponent <= 15 and len(result.as_tuple().digits) <= 30,
            f"{name} exceeds the supported input precision (12 fractional digits).")
    require(abs(result) <= MAX_INPUT, f"{name} exceeds the input magnitude limit of 1e15.")
    return result


def integer(value, name: str, minimum: int, maximum: int) -> int:
    result = number(value, name)
    require(result == result.to_integral_value() and minimum <= result <= maximum,
            f"{name} must be an integer from {minimum} to {maximum}.")
    return int(result)


def numeric(data: dict, name: str, minimum=None, maximum=None) -> Decimal:
    result = number(data[name], name)
    require(minimum is None or result >= minimum, f"{name} is below its allowed minimum.")
    require(maximum is None or result <= maximum, f"{name} exceeds its allowed maximum.")
    return result


def schema(data: dict, required: tuple[str, ...], optional: tuple[str, ...] = ()) -> None:
    missing = set(required) - data.keys()
    extra = data.keys() - {"operation", *required, *optional}
    require(not missing, "Missing required field(s): " + ", ".join(sorted(missing)))
    require(not extra, "Unknown field(s): " + ", ".join(sorted(extra)))


def choice(data: dict, name: str, choices: tuple[str, ...], default: str) -> str:
    value = data.get(name, default)
    require(isinstance(value, str) and value in choices, f"{name} must be one of: {', '.join(choices)}.")
    return value


def places_for(data: dict) -> int:
    return integer(data.get("places", 2), "places", 0, 6)


def rounded(value: Decimal, places: int, mode=ROUND_HALF_UP) -> Decimal:
    require(value.is_finite() and abs(value) <= MAX_RESULT, "Result exceeds the supported magnitude limit of 1e30.")
    return value.quantize(Decimal(1).scaleb(-places), rounding=mode)


def display(value: Decimal) -> str:
    value = rounded(value, 12)
    if not value:
        return "0"
    return format(value, "f").rstrip("0").rstrip(".")


def money(value: Decimal, places: int) -> str:
    value = rounded(value, places)
    return format(abs(value) if not value else value, f".{places}f")


def amount(data: dict, key: str, places: int) -> Decimal:
    return rounded(numeric(data, key, Decimal(0)), places)


def money_assumptions(places: int) -> list[str]:
    return [f"Money uses {places} decimal places and ROUND_HALF_UP.",
            "No tax or service-specific rules are applied."]


def percentages(data: dict, operation: str):
    if operation == "percent.part":
        schema(data, ("total", "rate_percent"))
        value = numeric(data, "total") * numeric(data, "rate_percent") / 100
        return {"value": display(value)}, ["rate_percent is a percentage, not a decimal fraction."]
    if operation == "percent.share":
        schema(data, ("part", "total"))
        total = numeric(data, "total")
        require(total != 0, "total must not be zero.")
        return {"percent": display(numeric(data, "part") / total * 100)}, ["Signed part / total * 100."]
    if operation == "percent.change":
        schema(data, ("before", "after"))
        before = numeric(data, "before")
        require(before > 0, "before must be positive; zero and negative baselines are unsupported.")
        return {"percent": display((numeric(data, "after") - before) / before * 100)}, ["The baseline is positive."]
    if operation == "percent.adjust":
        schema(data, ("value", "rate_percent"))
        return {"value": display(numeric(data, "value") * (1 + numeric(data, "rate_percent") / 100))}, ["Negative rates reduce the value."]
    schema(data, ("before_percent", "after_percent"))
    return {"percentage_points": display(numeric(data, "after_percent") - numeric(data, "before_percent"))}, ["This is a percentage-point difference, not a relative percentage change."]


def discounts(data: dict, operation: str):
    if operation == "discount.sequential":
        schema(data, ("price", "rates_percent"), ("places",))
        rates = data["rates_percent"]
        require(isinstance(rates, list) and 1 <= len(rates) <= 100, "rates_percent must contain 1 to 100 rates.")
        rates = [number(item, "rates_percent") for item in rates]
    else:
        schema(data, ("price", "rate_percent"), ("places",))
        rates = [numeric(data, "rate_percent")]
    require(all(0 <= rate <= 100 for rate in rates), "Discount rates must be from 0 to 100 percent.")
    places = places_for(data)
    price = amount(data, "price", places)
    if operation == "discount.reverse":
        require(rates[0] < 100, "A 100 percent discount cannot be reversed.")
        final = price
        original = rounded(final / (1 - rates[0] / 100), places)
    else:
        original = price
        final = price
        for rate in rates:
            final *= 1 - rate / 100
        final = rounded(final, places)
    assumptions = money_assumptions(places)
    assumptions.append("Discounts multiply sequentially; only the final price is rounded. Discount = rounded original - rounded final.")
    return {"original": money(original, places), "discount": money(original - final, places), "final": money(final, places)}, assumptions


def unit_price(data: dict):
    schema(data, ("total", "quantity"), ("per_quantity", "places"))
    places = places_for(data)
    quantity = numeric(data, "quantity")
    per = number(data.get("per_quantity", 1), "per_quantity")
    require(quantity > 0 and per > 0, "quantity and per_quantity must be positive.")
    value = amount(data, "total", places) / quantity * per
    return {"unit_price": money(value, places), "per_quantity": display(per)}, money_assumptions(places)


def bill_split(data: dict):
    schema(data, ("total", "weights"), ("places",))
    weights = data["weights"]
    require(isinstance(weights, list) and 1 <= len(weights) <= 100, "weights must contain 1 to 100 values.")
    weights = [number(value, "weights") for value in weights]
    require(all(value >= 0 for value in weights) and sum(weights) > 0, "weights must be nonnegative with a positive sum.")
    places = places_for(data)
    total = amount(data, "total", places)
    quantum = Decimal(1).scaleb(-places)
    units = int(total / quantum)
    exact_weights = [Fraction(value) for value in weights]
    exact = [units * value / sum(exact_weights) for value in exact_weights]
    allocated = [value.numerator // value.denominator for value in exact]
    order = sorted(range(len(weights)), key=lambda i: (-(exact[i] - allocated[i]), i))
    for index in order[:units - sum(allocated)]:
        allocated[index] += 1
    return {"total": money(total, places), "shares": [money(Decimal(value) * quantum, places) for value in allocated]}, money_assumptions(places) + ["Largest remainders receive leftover minor units; ties follow input order."]


def interest(data: dict, operation: str):
    optional = ("places", "compounds_per_year") if operation == "interest.compound" else ("places",)
    schema(data, ("principal", "annual_rate_percent", "years"), optional)
    places = places_for(data)
    principal = amount(data, "principal", places)
    rate = numeric(data, "annual_rate_percent", Decimal(0), Decimal(1000)) / 100
    years = numeric(data, "years", Decimal(0), Decimal(100))
    assumptions = money_assumptions(places)
    if operation == "interest.simple":
        earned = principal * rate * years
        assumptions.append("Simple interest uses user-supplied years; no bank day-count convention is inferred.")
    else:
        frequency = integer(data.get("compounds_per_year", 1), "compounds_per_year", 1, 366)
        count = frequency * years
        require(count == count.to_integral_value() and count <= MAX_PERIODS, "years * compounds_per_year must be a whole number from 0 to 1200.")
        earned = principal * ((1 + rate / frequency) ** int(count) - 1)
        assumptions.append("The annual rate is nominal; periodic rate = annual rate / compounds_per_year.")
    earned = rounded(earned, places)
    return {"principal": money(principal, places), "interest": money(earned, places), "balance": money(principal + earned, places)}, assumptions


def savings(data: dict, operation: str):
    extra = "contribution" if operation == "savings.recurring" else "target"
    schema(data, ("initial", extra, "annual_rate_percent", "periods", "periods_per_year"), ("timing", "places"))
    places = places_for(data)
    initial = amount(data, "initial", places)
    rate = numeric(data, "annual_rate_percent", Decimal(0), Decimal(1000)) / 100
    count = integer(data["periods"], "periods", 1, MAX_PERIODS)
    frequency = integer(data["periods_per_year"], "periods_per_year", 1, 366)
    timing = choice(data, "timing", ("end", "begin"), "end")
    r = rate / frequency
    factor = (1 + r) ** count
    annuity = Decimal(count) if r == 0 else (factor - 1) / r
    if timing == "begin":
        annuity *= 1 + r
    assumptions = money_assumptions(places) + ["The annual rate is nominal; interest compounds once per contribution period.", f"Contributions occur at the {timing} of each period; rates stay fixed."]
    if operation == "savings.target":
        target = amount(data, "target", places)
        contribution = rounded(max(Decimal(0), (target - initial * factor) / annuity), places, ROUND_CEILING)
        assumptions.append("Required contribution rounds upward to the selected minor unit; zero is returned if the initial balance already meets the target.")
    else:
        contribution = amount(data, "contribution", places)
    balance = rounded(initial * factor + contribution * annuity, places)
    principal = initial + contribution * count
    result = {"contribution": money(contribution, places), "principal_total": money(principal, places),
              "interest": money(balance - principal, places), "balance": money(balance, places)}
    if operation == "savings.target":
        result["target"] = money(target, places)
    return result, assumptions


def loan(data: dict):
    schema(data, ("principal", "annual_rate_percent", "periods", "periods_per_year", "method"), ("schedule", "places"))
    places = places_for(data)
    principal = amount(data, "principal", places)
    require(principal > 0, "principal must remain positive after money rounding.")
    count = integer(data["periods"], "periods", 1, MAX_PERIODS)
    frequency = integer(data["periods_per_year"], "periods_per_year", 1, 366)
    rate = numeric(data, "annual_rate_percent", Decimal(0), Decimal(1000)) / 100 / frequency
    method = choice(data, "method", ("annuity", "equal_principal", "bullet"), "annuity")
    schedule = choice(data, "schedule", ("summary", "full"), "summary")
    regular = rounded(principal / count if rate == 0 else principal * rate / (1 - (1 + rate) ** (-count)), places)
    fixed_principal = rounded(principal / count, places)
    balance, total_interest, total_payment = principal, Decimal(0), Decimal(0)
    rows = []
    for period in range(1, count + 1):
        opening = balance
        earned = rounded(balance * rate, places)
        if period == count:
            repaid = balance
        elif method == "bullet":
            repaid = Decimal(0)
        elif method == "equal_principal":
            repaid = min(balance, fixed_principal)
        else:
            require(regular >= earned, "The rounded regular payment does not cover interest.")
            repaid = min(balance, regular - earned)
        payment = repaid + earned
        balance -= repaid
        total_interest += earned
        total_payment += payment
        if schedule == "full":
            rows.append({"period": period, "opening_balance": money(opening, places), "interest": money(earned, places),
                         "principal": money(repaid, places), "payment": money(payment, places), "closing_balance": money(balance, places)})
    result = {"method": method, "principal": money(principal, places), "periods": count,
              "periodic_rate_percent": display(rate * 100), "total_interest": money(total_interest, places),
              "total_payment": money(total_payment, places), "final_balance": money(balance, places)}
    if method == "annuity":
        result["regular_payment"] = money(regular, places)
    if schedule == "full":
        result["schedule"] = rows
    return result, money_assumptions(places) + ["Fixed nominal annual rate divided by periods_per_year; equal-length periods with no fees or grace period.", "Interest and regular amounts round each period; the last principal payment reconciles the remaining balance."]


def parse_date(value, name: str) -> dt.date:
    require(isinstance(value, str) and DATE.fullmatch(value) is not None, f"{name} must use YYYY-MM-DD.")
    try:
        return dt.date.fromisoformat(value)
    except ValueError:
        raise CalculationError(f"{name} is not a valid Gregorian date.") from None


def shifted_months(value: dt.date, months: int) -> dt.date:
    index = (value.year - 1) * 12 + value.month - 1 + months
    require(0 <= index < 9999 * 12, "Date result is outside years 0001 through 9999.")
    year, month = divmod(index, 12)
    year, month = year + 1, month + 1
    return dt.date(year, month, min(value.day, calendar.monthrange(year, month)[1]))


def dates(data: dict, operation: str):
    assumptions = ["Proleptic Gregorian calendar; no holidays, lunar dates, or legal age rules."]
    if operation == "date.diff":
        schema(data, ("start", "end"), ("inclusion",))
        start, end = parse_date(data["start"], "start"), parse_date(data["end"], "end")
        mode = choice(data, "inclusion", ("elapsed", "both"), "elapsed")
        require(mode != "both" or end >= start, "both requires end on or after start.")
        return {"days": (end - start).days + (1 if mode == "both" else 0)}, assumptions + [f"Counting mode: {mode}."]
    if operation == "date.add":
        schema(data, ("date",), ("years", "months", "days"))
        value = parse_date(data["date"], "date")
        years = integer(data.get("years", 0), "years", -9999, 9999)
        months = integer(data.get("months", 0), "months", -119988, 119988)
        days = integer(data.get("days", 0), "days", -3652058, 3652058)
        value = shifted_months(value, years * 12 + months) + dt.timedelta(days=days)
        return {"date": value.isoformat()}, assumptions + ["Years and months apply together to the original date, clamped to the target month's last day; days apply afterward. Repeated schedules should always use the original anchor."]
    if operation == "date.age":
        schema(data, ("birth_date", "as_of"), ("leap_day",))
        birth, as_of = parse_date(data["birth_date"], "birth_date"), parse_date(data["as_of"], "as_of")
        require(birth <= as_of, "birth_date must not be after as_of.")
        leap_day = choice(data, "leap_day", ("feb28", "mar1"), "feb28")
        anniversary = shifted_months(birth, (as_of.year - birth.year) * 12)
        if birth.month == 2 and birth.day == 29 and not calendar.isleap(as_of.year) and leap_day == "mar1":
            anniversary = dt.date(as_of.year, 3, 1)
        return {"years": as_of.year - birth.year - int(as_of < anniversary)}, assumptions + [f"A February 29 birthday uses {leap_day} in non-leap years; this is a calculator convention."]
    schema(data, ("date",))
    value = parse_date(data["date"], "date")
    return {"iso_weekday": value.isoweekday(), "weekday": ("Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday")[value.weekday()]}, assumptions


def offset(value, name: str) -> dt.timezone:
    match = OFFSET.fullmatch(value) if isinstance(value, str) else None
    require(match is not None, f"{name} must use a signed fixed UTC offset +/-HH:MM below 24 hours.")
    sign, hours, minutes = match.groups()
    total = (int(hours) * 60 + int(minutes)) * (-1 if sign == "-" else 1)
    return dt.timezone(dt.timedelta(minutes=total))


def time_conversion(data: dict):
    schema(data, ("datetime", "from_offset", "to_offset"))
    value = data["datetime"]
    require(isinstance(value, str) and DATETIME.fullmatch(value) is not None, "datetime must use YYYY-MM-DDTHH:MM[:SS[.ffffff]] without an offset.")
    hour, minute = int(value[11:13]), int(value[14:16])
    second = int(value[17:19]) if len(value) >= 19 else 0
    require(hour < 24 and minute < 60 and second < 60, "Hours must be 00 through 23; minutes and seconds must be 00 through 59.")
    try:
        microsecond = int(value[20:].ljust(6, "0")) if len(value) > 19 else 0
        value = dt.datetime(int(value[:4]), int(value[5:7]), int(value[8:10]), hour, minute, second, microsecond)
    except ValueError:
        raise CalculationError("datetime is not a valid Gregorian date and time.") from None
    source = value.replace(tzinfo=offset(data["from_offset"], "from_offset"))
    converted = source.astimezone(offset(data["to_offset"], "to_offset"))
    return {"source": source.isoformat(), "converted": converted.isoformat(), "utc": source.astimezone(dt.timezone.utc).isoformat()}, ["Offsets are fixed; no city names, IANA time zones, DST, or leap seconds are inferred."]


def units(data: dict):
    schema(data, ("value", "from_unit", "to_unit"))
    # Each factor converts to the family's base unit. Temperature is affine.
    groups = {
        "length": {"mm": Decimal(".001"), "cm": Decimal(".01"), "m": Decimal(1), "km": Decimal(1000), "in": Decimal(".0254"), "ft": Decimal(".3048"), "yd": Decimal(".9144"), "mi": Decimal("1609.344")},
        "mass": {"mg": Decimal(".000001"), "g": Decimal(".001"), "kg": Decimal(1), "oz": Decimal(".028349523125"), "lb": Decimal(".45359237")},
        "area": {"sqm": Decimal(1), "sqft": Decimal(".09290304"), "pyeong": Decimal(400) / 121},
        "volume": {"ml": Decimal(".000001"), "l": Decimal(".001"), "m3": Decimal(1)},
        "speed": {"m/s": Decimal(1), "km/h": Decimal(5) / 18, "mph": Decimal(".44704")},
        "duration": {"s": Decimal(1), "min": Decimal(60), "h": Decimal(3600), "day": Decimal(86400), "week": Decimal(604800)},
        "temperature": {"C": Decimal(1), "F": Decimal(1), "K": Decimal(1)},
    }
    source, target = data["from_unit"], data["to_unit"]
    require(isinstance(source, str) and isinstance(target, str), "Unit identifiers must be strings.")
    family = next((name for name, members in groups.items() if source in members), None)
    require(family is not None and target in groups[family], "Units must be known identifiers in the same family.")
    value = numeric(data, "value")
    if family == "temperature":
        celsius = (value - 32) * 5 / 9 if source == "F" else value - Decimal("273.15") if source == "K" else value
        require(celsius >= Decimal("-273.15"), "Temperature cannot be below absolute zero.")
        result = celsius * 9 / 5 + 32 if target == "F" else celsius + Decimal("273.15") if target == "K" else celsius
    else:
        result = value * groups[family][source] / groups[family][target]
    return {"value": display(result), "unit": target, "family": family}, ["Fixed conversion factors only; pyeong = 400/121 square metres. Duration days are exactly 24 hours; months and years are not duration units."]


def bmi(data: dict):
    schema(data, ("weight_kg", "height_cm"))
    weight, height = numeric(data, "weight_kg"), numeric(data, "height_cm")
    require(weight > 0 and height > 0, "weight_kg and height_cm must be positive.")
    result = weight / (height / 100) ** 2
    return {"bmi": money(result, 2)}, ["BMI = weight in kg / height in metres squared, rounded to 2 decimal places.", "Number only: no diagnosis, weight-status classification, child percentile, or treatment advice."]


def calculate(request: dict) -> dict:
    require(isinstance(request, dict), "The input must be one JSON object.")
    operation = request.get("operation")
    require(isinstance(operation, str), "operation must be a string.")
    with localcontext() as context:
        context.prec, context.Emax, context.Emin = 50, 99, -99
        if operation in ("percent.part", "percent.share", "percent.change", "percent.adjust", "percent.points"):
            result, assumptions = percentages(request, operation)
        elif operation in ("discount.single", "discount.sequential", "discount.reverse"):
            result, assumptions = discounts(request, operation)
        elif operation == "unit_price":
            result, assumptions = unit_price(request)
        elif operation == "bill_split":
            result, assumptions = bill_split(request)
        elif operation in ("interest.simple", "interest.compound"):
            result, assumptions = interest(request, operation)
        elif operation in ("savings.recurring", "savings.target"):
            result, assumptions = savings(request, operation)
        elif operation == "loan":
            result, assumptions = loan(request)
        elif operation in ("date.diff", "date.add", "date.age", "date.weekday"):
            result, assumptions = dates(request, operation)
        elif operation == "time.convert":
            result, assumptions = time_conversion(request)
        elif operation == "unit.convert":
            result, assumptions = units(request)
        elif operation == "bmi":
            result, assumptions = bmi(request)
        else:
            raise CalculationError("Unknown operation.")
    return {"ok": True, "operation": operation, "result": result, "assumptions": assumptions}


def json_number(text: str) -> Decimal:
    require(len(text) <= 80, "A JSON number exceeds the supported input length.")
    return Decimal(text)


def reject_constant(_value):
    raise CalculationError("NaN and Infinity are not supported.")


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "Duplicate JSON object keys are not supported.")
        result[key] = value
    return result


def check_structure(value, depth=0):
    require(depth <= 3, "Nested inputs are not supported.")
    require(not isinstance(value, bool) and value is not None, "Boolean and null inputs are not supported.")
    if isinstance(value, dict):
        require(len(value) <= 20, "Too many input fields.")
        for key, item in value.items():
            require(len(key) <= 64, "An input field name is too long.")
            check_structure(item, depth + 1)
    elif isinstance(value, list):
        require(len(value) <= MAX_PERIODS, "Input array is too long.")
        for item in value:
            check_structure(item, depth + 1)
    elif isinstance(value, str):
        require(len(value) <= 128, "An input string is too long.")


def process_payload(raw: bytes) -> tuple[dict, int]:
    try:
        require(len(raw) <= MAX_INPUT_BYTES, "Input exceeds 65536 bytes.")
        request = json.loads(raw.decode("utf-8"), parse_int=json_number, parse_float=json_number,
                             parse_constant=reject_constant, object_pairs_hook=unique_object)
        check_structure(request)
        response = calculate(request)
        require(len(json.dumps(response).encode("utf-8")) <= MAX_OUTPUT_BYTES, "Output exceeds the supported size.")
        return response, 0
    except CalculationError as error:
        return {"ok": False, "error": {"code": "invalid_input", "message": str(error)}}, 2
    except (UnicodeDecodeError, json.JSONDecodeError, RecursionError):
        return {"ok": False, "error": {"code": "invalid_json", "message": "Input must be one valid UTF-8 JSON object."}}, 2
    except (DecimalException, OverflowError, ValueError):
        return {"ok": False, "error": {"code": "out_of_range", "message": "The calculation exceeds the supported numeric or date range."}}, 2


def main() -> int:
    if len(sys.argv) != 1:
        response, status = {"ok": False, "error": {"code": "invalid_input", "message": "Pass one JSON object through stdin; command-line arguments are unsupported."}}, 2
    else:
        response, status = process_payload(sys.stdin.buffer.read(MAX_INPUT_BYTES + 1))
    sys.stdout.write(json.dumps(response, ensure_ascii=True, allow_nan=False, separators=(",", ":")) + "\n")
    return status


if __name__ == "__main__":
    raise SystemExit(main())
