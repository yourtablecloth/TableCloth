"""Behavior tests; no services, files, or child processes are used by calculators."""

import io
import json
from decimal import Decimal
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import calculate as engine


class CalculatorTests(unittest.TestCase):
    def request(self, operation, **fields):
        result, status = engine.process_payload(json.dumps({"operation": operation, **fields}).encode())
        self.assertEqual(status, 0, result)
        self.assertTrue(result["ok"])
        self.assertTrue(result["assumptions"])
        return result["result"]

    def invalid(self, operation, **fields):
        result, status = engine.process_payload(json.dumps({"operation": operation, **fields}).encode())
        self.assertEqual(status, 2, result)
        self.assertFalse(result["ok"])
        self.assertIn(result["error"]["code"], ("invalid_input", "invalid_json", "out_of_range"))
        self.assertTrue(result["error"]["message"])
        return result

    def test_percentages_and_points_are_distinct(self):
        self.assertEqual(self.request("percent.part", total="200", rate_percent="12.5"), {"value": "25"})
        self.assertEqual(self.request("percent.share", part="1", total="4"), {"percent": "25"})
        self.assertEqual(self.request("percent.change", before="100", after="90"), {"percent": "-10"})
        self.assertEqual(self.request("percent.adjust", value="100", rate_percent="-20"), {"value": "80"})
        self.assertEqual(self.request("percent.points", before_percent="5", after_percent="6"), {"percentage_points": "1"})
        self.assertEqual(self.request("percent.change", before="5", after="6"), {"percent": "20"})

    def test_percent_baseline_and_required_fields(self):
        for before in ("0", "-100"):
            self.invalid("percent.change", before=before, after="10")
        self.invalid("percent.share", part="1", total="0")
        self.invalid("percent.part", total="100")
        self.invalid("percent.part", total="100", rate_percent="1", extra="ignored")

    def test_number_input_forms_and_precision(self):
        self.assertEqual(self.request("percent.part", total=200, rate_percent=12.5), {"value": "25"})
        self.assertEqual(self.request("percent.part", total="1e3", rate_percent="1e-1"), {"value": "1"})
        self.assertEqual(self.request("percent.adjust", value="0", rate_percent="-100"), {"value": "0"})
        for value in (True, False, None, "NaN", "Infinity", "-Infinity", "1e100000", "1e-100000", "1.0000000000001", "1e16", [], {}, " 1", "1,000", "2+3", "__import__('os')"):
            with self.subTest(value=value):
                self.invalid("percent.part", total=value, rate_percent="5")

    def test_discounts_and_reverse(self):
        self.assertEqual(self.request("discount.single", price="100", rate_percent="20"),
                         {"original": "100.00", "discount": "20.00", "final": "80.00"})
        self.assertEqual(self.request("discount.sequential", price="100", rates_percent=["20", "10"])["final"], "72.00")
        self.assertEqual(self.request("discount.reverse", price="80", rate_percent="20")["original"], "100.00")
        self.assertEqual(self.request("discount.single", price="100", rate_percent="100")["final"], "0.00")
        self.invalid("discount.reverse", price="0", rate_percent="100")
        self.invalid("discount.single", price="100", rate_percent="-1")
        self.invalid("discount.single", price="100", rate_percent="101")
        self.invalid("discount.sequential", price="100", rates_percent=[])
        self.invalid("discount.sequential", price="100", rates_percent=["1"] * 101)

    def test_discount_rounding_reconciles_total(self):
        result = self.request("discount.single", price="1", rate_percent="99.5")
        self.assertEqual(result["final"], "0.01")
        self.assertEqual(result["discount"], "0.99")
        self.assertEqual(Decimal(result["original"]), Decimal(result["discount"]) + Decimal(result["final"]))
        self.assertEqual(self.request("discount.single", price="100", rate_percent="12.5", places=0)["final"], "88")

    def test_unit_price(self):
        self.assertEqual(self.request("unit_price", total="12", quantity="500", per_quantity="100")["unit_price"], "2.40")
        self.invalid("unit_price", total="10", quantity="0")
        self.invalid("unit_price", total="-10", quantity="2")
        self.invalid("unit_price", total="10", quantity="2", per_quantity="-1")

    def test_bill_split_conserves_minor_units_and_tie_order(self):
        self.assertEqual(self.request("bill_split", total="100", weights=["1", "2"])["shares"], ["33.33", "66.67"])
        self.assertEqual(self.request("bill_split", total="0.01", weights=["1", "1", "1"])["shares"], ["0.01", "0.00", "0.00"])
        self.assertEqual(self.request("bill_split", total="1", weights=["0", "1", "2"], places=0)["shares"], ["0", "0", "1"])
        for weights in (["0", "0"], ["-1", "2"], [], ["1"] * 101):
            self.invalid("bill_split", total="100", weights=weights)

    def test_bill_split_large_and_fractional_weights(self):
        for total, weights in [("1000000000000000", ["0.000000000001", "999999999999999", "1"]),
                               ("0.07", ["1", "1", "1", "1", "1", "1"]),
                               ("1234.567891", ["0.001", "0.002", "0.003"])]:
            with self.subTest(total=total):
                result = self.request("bill_split", total=total, weights=weights, places=6)
                self.assertEqual(sum(map(Decimal, result["shares"])), Decimal(result["total"]))
                self.assertTrue(all(Decimal(value) >= 0 for value in result["shares"]))

    def test_simple_and_compound_interest(self):
        self.assertEqual(self.request("interest.simple", principal="1000000", annual_rate_percent="5", years="2")["interest"], "100000.00")
        self.assertEqual(self.request("interest.compound", principal="1000000", annual_rate_percent="10", years="2")["balance"], "1210000.00")
        self.assertEqual(self.request("interest.compound", principal="100", annual_rate_percent="0", years="2", compounds_per_year=12)["balance"], "100.00")
        self.assertEqual(self.request("interest.compound", principal="100", annual_rate_percent="10", years="0")["balance"], "100.00")
        self.assertEqual(self.request("interest.simple", principal="100", annual_rate_percent="10", years="0.5")["interest"], "5.00")
        self.invalid("interest.compound", principal="100", annual_rate_percent="10", years="0.1", compounds_per_year=12)
        self.invalid("interest.compound", principal="100", annual_rate_percent="10", years="100", compounds_per_year=365)
        self.invalid("interest.simple", principal="100", annual_rate_percent="-1", years="2")

    def test_recurring_savings_timing_and_zero_rate(self):
        common = dict(initial="100", contribution="10", annual_rate_percent="10", periods=2, periods_per_year=1)
        end = self.request("savings.recurring", **common)
        begin = self.request("savings.recurring", **common, timing="begin")
        self.assertEqual(end["balance"], "142.00")
        self.assertEqual(begin["balance"], "144.10")
        self.assertEqual(end["principal_total"], "120.00")
        self.assertEqual(end["interest"], "22.00")
        common["annual_rate_percent"] = "0"
        self.assertEqual(self.request("savings.recurring", **common)["balance"], "120.00")

    def test_target_contribution_rounds_up_and_never_negative(self):
        common = dict(initial="0", target="100", annual_rate_percent="0", periods=3, periods_per_year=12)
        result = self.request("savings.target", **common)
        self.assertEqual(result["contribution"], "33.34")
        self.assertEqual(result["balance"], "100.02")
        common["initial"] = "200"
        self.assertEqual(self.request("savings.target", **common)["contribution"], "0.00")
        common["periods"] = 0
        self.invalid("savings.target", **common)

    def assert_schedule(self, result):
        rows = result["schedule"]
        self.assertEqual(len(rows), result["periods"])
        self.assertEqual(sum(Decimal(row["principal"]) for row in rows), Decimal(result["principal"]))
        self.assertEqual(sum(Decimal(row["interest"]) for row in rows), Decimal(result["total_interest"]))
        self.assertEqual(sum(Decimal(row["payment"]) for row in rows), Decimal(result["total_payment"]))
        self.assertEqual(Decimal(result["total_payment"]), Decimal(result["principal"]) + Decimal(result["total_interest"]))
        self.assertEqual(Decimal(result["final_balance"]), 0)
        for i, row in enumerate(rows):
            self.assertEqual(row["period"], i + 1)
            self.assertEqual(Decimal(row["opening_balance"]) - Decimal(row["principal"]), Decimal(row["closing_balance"]))
            self.assertEqual(Decimal(row["principal"]) + Decimal(row["interest"]), Decimal(row["payment"]))
            self.assertTrue(all(Decimal(row[key]) >= 0 for key in ("principal", "interest", "payment", "closing_balance")))
            if i:
                self.assertEqual(rows[i - 1]["closing_balance"], row["opening_balance"])

    def test_three_loan_methods_reconcile_and_have_correct_structure(self):
        common = dict(principal="1200", annual_rate_percent="12", periods=3, periods_per_year=12, schedule="full")
        for method in ("annuity", "equal_principal", "bullet"):
            with self.subTest(method=method):
                result = self.request("loan", **common, method=method)
                self.assert_schedule(result)
                if method == "equal_principal":
                    self.assertEqual([row["principal"] for row in result["schedule"]], ["400.00"] * 3)
                    self.assertEqual([row["interest"] for row in result["schedule"]], ["12.00", "8.00", "4.00"])
                if method == "bullet":
                    self.assertEqual([row["principal"] for row in result["schedule"]], ["0.00", "0.00", "1200.00"])
                    self.assertEqual(result["total_interest"], "36.00")

    def test_zero_rate_loan_last_payment_reconciles(self):
        result = self.request("loan", principal="1000", annual_rate_percent="0", periods=3, periods_per_year=12, method="annuity", schedule="full")
        self.assert_schedule(result)
        self.assertEqual([row["payment"] for row in result["schedule"]], ["333.33", "333.33", "333.34"])

    def test_loan_one_period_tiny_rate_and_term_bounds(self):
        for principal, rate, periods in (("0.01", "0", 1200), ("100", "0.000000000001", 12), ("1000", "1000", 1)):
            result = self.request("loan", principal=principal, annual_rate_percent=rate, periods=periods, periods_per_year=12, method="annuity", schedule="full")
            self.assert_schedule(result)
        for periods in (0, -1, 1201, "1.5", True):
            self.invalid("loan", principal="100", annual_rate_percent="5", periods=periods, periods_per_year=12, method="annuity")
        self.invalid("loan", principal="0.001", annual_rate_percent="5", periods=12, periods_per_year=12, method="annuity")
        self.invalid("loan", principal="100", annual_rate_percent="5", periods=12, periods_per_year=0, method="annuity")

    def test_loan_summary_has_no_schedule(self):
        result = self.request("loan", principal="1200", annual_rate_percent="0", periods=12, periods_per_year=12, method="annuity")
        self.assertNotIn("schedule", result)
        self.assertEqual(result["regular_payment"], "100.00")

    def test_date_difference_and_inclusion(self):
        common = dict(start="2024-02-28", end="2024-03-01")
        self.assertEqual(self.request("date.diff", **common)["days"], 2)
        self.assertEqual(self.request("date.diff", **common, inclusion="both")["days"], 3)
        self.assertEqual(self.request("date.diff", start="2025-01-01", end="2025-01-01")["days"], 0)
        self.assertEqual(self.request("date.diff", start="2025-01-01", end="2025-01-01", inclusion="both")["days"], 1)
        self.assertEqual(self.request("date.diff", start="2025-01-02", end="2025-01-01")["days"], -1)
        self.invalid("date.diff", start="2025-01-02", end="2025-01-01", inclusion="both")

    def test_month_end_uses_original_anchor_and_combined_year_month_shift(self):
        self.assertEqual(self.request("date.add", date="2025-01-31", months=1)["date"], "2025-02-28")
        self.assertEqual(self.request("date.add", date="2025-01-31", months=2)["date"], "2025-03-31")
        self.assertEqual(self.request("date.add", date="2024-02-29", years=1)["date"], "2025-02-28")
        self.assertEqual(self.request("date.add", date="2024-02-29", years=1, months=1)["date"], "2025-03-29")
        self.assertEqual(self.request("date.add", date="2025-03-31", months=-1, days=1)["date"], "2025-03-01")

    def test_age_and_weekday(self):
        self.assertEqual(self.request("date.age", birth_date="2000-02-29", as_of="2025-02-28")["years"], 25)
        self.assertEqual(self.request("date.age", birth_date="2000-02-29", as_of="2025-02-28", leap_day="mar1")["years"], 24)
        self.assertEqual(self.request("date.age", birth_date="2025-02-28", as_of="2025-02-28")["years"], 0)
        self.assertEqual(self.request("date.weekday", date="2024-02-29"), {"iso_weekday": 4, "weekday": "Thursday"})
        self.invalid("date.age", birth_date="2026-01-01", as_of="2025-01-01")

    def test_invalid_dates_and_limits(self):
        for value in ("1900-02-29", "2025-02-29", "2025-13-01", "0000-01-01", "20250101", "2025-W01-1", "2025-1-1"):
            self.invalid("date.weekday", date=value)
        self.assertEqual(self.request("date.weekday", date="2000-02-29")["iso_weekday"], 2)
        self.invalid("date.add", date="9999-12-31", days=1)
        self.invalid("date.add", date="0001-01-01", months=-1)
        self.invalid("date.add", date="2025-01-01", years=10000)
        self.invalid("date.add", date="2025-01-01", days="1.2")

    def test_fixed_offset_fractional_and_date_crossovers(self):
        result = self.request("time.convert", datetime="2026-12-31T22:30", from_offset="+00:00", to_offset="+09:00")
        self.assertEqual(result["converted"], "2027-01-01T07:30:00+09:00")
        self.assertEqual(self.request("time.convert", datetime="2026-10-25T12:00", from_offset="+05:45", to_offset="+09:00")["converted"], "2026-10-25T15:15:00+09:00")
        self.assertEqual(self.request("time.convert", datetime="2026-01-01T01:00", from_offset="+09:00", to_offset="-03:30")["converted"], "2025-12-31T12:30:00-03:30")

    def test_same_wall_clock_can_have_two_explicit_offsets(self):
        first = self.request("time.convert", datetime="2020-11-01T01:00", from_offset="-07:00", to_offset="+00:00")
        second = self.request("time.convert", datetime="2020-11-01T01:00", from_offset="-08:00", to_offset="+00:00")
        self.assertEqual(first["converted"], "2020-11-01T08:00:00+00:00")
        self.assertEqual(second["converted"], "2020-11-01T09:00:00+00:00")

    def test_named_zones_invalid_offsets_and_leap_seconds_are_rejected(self):
        for offset in ("America/New_York", "UTC", "+24:00", "+09:60", "+9:00", 9):
            self.invalid("time.convert", datetime="2025-01-01T00:00", from_offset=offset, to_offset="+00:00")
        for value in ("2025-01-01T24:00", "2025-01-01T00:00:60", "2025-01-01 00:00", "2025-01-01T00:00Z"):
            self.invalid("time.convert", datetime=value, from_offset="+00:00", to_offset="+09:00")
        self.invalid("time.convert", datetime="0001-01-01T00:00", from_offset="+23:59", to_offset="-23:59")

    def test_fixed_units_and_affine_temperatures(self):
        for value, source, target, expected in (("1", "m", "cm", "100"), ("1", "mi", "m", "1609.344"),
                                                 ("1", "lb", "kg", "0.45359237"), ("32", "F", "C", "0"),
                                                 ("0", "C", "K", "273.15"), ("1", "l", "ml", "1000"),
                                                 ("36", "km/h", "m/s", "10"), ("1", "day", "h", "24"),
                                                 ("1", "pyeong", "sqm", "3.305785123967")):
            with self.subTest(source=source, target=target):
                self.assertEqual(self.request("unit.convert", value=value, from_unit=source, to_unit=target)["value"], expected)
        self.assertEqual(self.request("unit.convert", value="400", from_unit="sqm", to_unit="pyeong")["value"], "121")

    def test_unit_dimension_and_physical_temperature_limits(self):
        self.invalid("unit.convert", value="1", from_unit="kg", to_unit="m")
        self.invalid("unit.convert", value="1", from_unit="month", to_unit="day")
        self.invalid("unit.convert", value="-0.01", from_unit="K", to_unit="C")
        self.invalid("unit.convert", value="-273.16", from_unit="C", to_unit="F")
        self.assertEqual(self.request("unit.convert", value="-459.67", from_unit="F", to_unit="K")["value"], "0")

    def test_bmi_is_a_number_without_classification(self):
        self.assertEqual(self.request("bmi", weight_kg="70", height_cm="175"), {"bmi": "22.86"})
        for weight, height in (("0", "175"), ("70", "0"), ("-1", "175"), ("70", "-175")):
            self.invalid("bmi", weight_kg=weight, height_cm=height)
        self.invalid("bmi", weight_kg="70", height_cm="175", age=10)

    def test_nonfinite_duplicate_unknown_or_malformed_json(self):
        for raw in (b'{"operation":"percent.part","total":NaN,"rate_percent":1}',
                    b'{"operation":"percent.part","total":Infinity,"rate_percent":1}',
                    b'{"operation":"percent.part","total":1,"total":2,"rate_percent":1}',
                    b'{"operation":"date.add","date":"2025-01-01","x":{"a":1,"a":2}}',
                    b'{}', b'[]', b'null', b'true', b'{} {}', b'{', b'\xff', b'1e9999',
                    b'{"operation":"__import__"}', b'{"operation":"tax"}', b'{"operation":"loan","schedule":true}'):
            with self.subTest(raw=raw):
                response, status = engine.process_payload(raw)
                self.assertEqual(status, 2)
                self.assertFalse(response["ok"])

    def test_input_precision_size_and_result_bounds(self):
        for raw in (b' ' * (engine.MAX_INPUT_BYTES + 1), b'{"operation":"' + b'x' * 129 + b'"}',
                    b'{"operation":"x","items":' + b'[' * 10 + b'0' + b']' * 10 + b'}'):
            self.assertEqual(engine.process_payload(raw)[1], 2)
        self.invalid("interest.compound", principal="1000000000000000", annual_rate_percent="1000", years="100", compounds_per_year=1)
        self.invalid("percent.part", total="1" * 81, rate_percent="1")
        self.invalid("discount.single", price="1", rate_percent="1", places=7)

    def test_main_writes_one_json_line_and_uses_status_code(self):
        raw = b'{"operation":"discount.single","price":"100","rate_percent":"20"}'
        input_stream = io.TextIOWrapper(io.BytesIO(raw), encoding="utf-8")
        output_stream = io.StringIO()
        with patch.object(sys, "argv", ["calculate.py"]), patch.object(sys, "stdin", input_stream), patch.object(sys, "stdout", output_stream):
            status = engine.main()
        self.assertEqual(status, 0)
        self.assertEqual(output_stream.getvalue().count("\n"), 1)
        self.assertEqual(json.loads(output_stream.getvalue())["result"]["final"], "80.00")
        output_stream = io.StringIO()
        with patch.object(sys, "argv", ["calculate.py", "arbitrary.py"]), patch.object(sys, "stdout", output_stream):
            self.assertEqual(engine.main(), 2)
        self.assertFalse(json.loads(output_stream.getvalue())["ok"])


if __name__ == "__main__":
    unittest.main()
