#!/usr/bin/env python3
"""
check_rule_tables.py -- linter for the StudPlacer code-rule CSVs.

Runs with nothing but a Python interpreter, so it works on a workstation with
no .NET SDK. Use it after transcribing a new drawing revision, before the full
suite in ../tests.

It deliberately does NOT re-implement the engine -- that would be a second
source of truth that quietly drifts. It checks the tables themselves:

  * well-formed, required columns present, numbers parse under any locale
  * S_T_DIVISOR == lines + 1 on every row (the identity the engine relies on)
  * derived S_t = W_sc / divisor reproduces the value PRINTED on G103
  * floor bands are ordered ascending and leave no gap in W_sc coverage
  * elevation bands are ordered and contiguous per wall component
  * every constraint the engine looks up by name actually exists

Exit 0 = clean, 1 = problems found.
"""
import csv, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
RULES = os.path.join(HERE, "..", "rules")

problems = []
checks = 0


def fail(msg):
    problems.append(msg)


def check(cond, msg):
    global checks
    checks += 1
    if not cond:
        fail(msg)


def load(name):
    path = os.path.join(RULES, name)
    rows, header = [], None
    with open(path, encoding="utf-8") as f:
        for lineno, raw in enumerate(f, 1):
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            fields = next(csv.reader([line]))
            if header is None:
                header = [h.strip().upper() for h in fields]
            else:
                if len(fields) != len(header):
                    fail(f"{name}:{lineno}: {len(fields)} fields, header has {len(header)}")
                rows.append((lineno, dict(zip(header, [x.strip() for x in fields]))))
    return header, rows


def num(name, lineno, row, col):
    try:
        return float(row[col])
    except (KeyError, ValueError):
        fail(f"{name}:{lineno}: column {col} is not a number: {row.get(col)!r}")
        return None


# --------------------------------------------------------------- Table 1-6
REQ16 = ["COMPONENT", "ELEV_FROM_M", "ELEV_TO_M", "T_SC_MM", "PLATE_GRADE", "W_SC_MAX_MM",
         "STUD_MATERIAL", "STUD_LEN_MM", "STUD_DIA_MM", "STUD_COLUMNS", "S_L_MAX_MM",
         "S_T_DIVISOR", "S_T_MAX_MM", "SOURCE"]
h16, r16 = load("table_1_6_walls.csv")
for c in REQ16:
    check(c in h16, f"table_1_6_walls.csv: missing column {c}")

by_comp = {}
for lineno, row in r16:
    comp = row["COMPONENT"]
    cols = num("table_1_6_walls.csv", lineno, row, "STUD_COLUMNS")
    div = num("table_1_6_walls.csv", lineno, row, "S_T_DIVISOR")
    wmax = num("table_1_6_walls.csv", lineno, row, "W_SC_MAX_MM")
    stmax = num("table_1_6_walls.csv", lineno, row, "S_T_MAX_MM")
    sl = num("table_1_6_walls.csv", lineno, row, "S_L_MAX_MM")
    dia = num("table_1_6_walls.csv", lineno, row, "STUD_DIA_MM")
    lo = num("table_1_6_walls.csv", lineno, row, "ELEV_FROM_M")
    hi = num("table_1_6_walls.csv", lineno, row, "ELEV_TO_M")
    if None in (cols, div, wmax, stmax, sl, dia, lo, hi):
        continue

    check(div == cols + 1,
          f"{comp} @{lineno}: S_T_DIVISOR {div:g} should be STUD_COLUMNS+1 = {cols+1:g}. "
          f"n stud lines between two diaphragms make n+1 gaps.")
    st = wmax / div
    check(st <= stmax + 0.01,
          f"{comp} @{lineno}: derived S_t at max W_sc is {st:.1f} mm, above the printed "
          f"ceiling {stmax:.1f} mm.")
    check(st >= 76.2 - 0.01,
          f"{comp} @{lineno}: derived S_t {st:.1f} mm is below the 76.2 mm AISC minimum.")
    check(sl >= 76.2 - 0.01,
          f"{comp} @{lineno}: S_L_MAX_MM {sl:.1f} mm is below the 76.2 mm AISC minimum, "
          f"so no legal pitch exists.")
    check(dia in (19.1, 12.7),
          f"{comp} @{lineno}: unexpected stud diameter {dia}")
    check(lo < hi, f"{comp} @{lineno}: elevation band runs {lo} -> {hi}; FROM must be lower.")
    by_comp.setdefault(comp, []).append((lo, hi, lineno))

for comp, bands in by_comp.items():
    bands.sort()
    for i in range(1, len(bands)):
        prev_hi = bands[i - 1][1]
        this_lo = bands[i][0]
        check(abs(prev_hi - this_lo) < 1e-6,
              f"{comp}: elevation bands leave a gap or overlap between {prev_hi} and {this_lo} m.")

# --------------------------------------------------------------- Table 1-7
REQ17 = ["COMPONENT", "T_SC_MM", "PLATE_GRADE", "W_SC_MAX_MM", "CMP", "STUD_MATERIAL",
         "STUD_LEN_MM", "STUD_DIA_MM", "STUD_ROWS", "S_L_MAX_MM", "S_T_DIVISOR", "SOURCE"]
h17, r17 = load("table_1_7_floors.csv")
for c in REQ17:
    check(c in h17, f"table_1_7_floors.csv: missing column {c}")

floors = {}
for lineno, row in r17:
    comp = row["COMPONENT"]
    rows_ = num("table_1_7_floors.csv", lineno, row, "STUD_ROWS")
    div = num("table_1_7_floors.csv", lineno, row, "S_T_DIVISOR")
    wmax = num("table_1_7_floors.csv", lineno, row, "W_SC_MAX_MM")
    sl = num("table_1_7_floors.csv", lineno, row, "S_L_MAX_MM")
    if None in (rows_, div, wmax, sl):
        continue
    check(row["CMP"].upper() in ("LT", "LE"),
          f"{comp} @{lineno}: CMP must be LT or LE, got {row['CMP']!r}")
    if rows_ > 0:
        check(div == rows_ + 1,
              f"{comp} @{lineno}: S_T_DIVISOR {div:g} should be STUD_ROWS+1 = {rows_+1:g}")
        st = wmax / div
        check(st >= 76.2 - 0.01,
              f"{comp} @{lineno}: derived S_t {st:.1f} mm is below the 76.2 mm AISC minimum.")
        check(st <= 254.0 + 0.01,
              f"{comp} @{lineno}: derived S_t {st:.1f} mm exceeds the 254 mm floor-array "
              f"ceiling (T1-7 n2).")
        check(sl >= 76.2 - 0.01,
              f"{comp} @{lineno}: S_L_MAX_MM {sl:.1f} mm is below the 76.2 mm AISC minimum.")
    else:
        check(div == 0, f"{comp} @{lineno}: a No-Studs band should have S_T_DIVISOR 0")
    floors.setdefault(comp, []).append((wmax, rows_, lineno))

for comp, bands in floors.items():
    check(bands == sorted(bands),
          f"{comp}: Table 1-7 bands must be listed in ascending W_SC_MAX_MM; the engine "
          f"takes the first match.")
    check(bands[0][1] == 0,
          f"{comp}: the lowest band should be the No-Studs band.")
    monotonic = all(bands[i][1] >= bands[i - 1][1] for i in range(1, len(bands)))
    check(monotonic,
          f"{comp}: stud rows must not decrease as W_sc grows.")

# ---- the printed G103 values, as an independent cross-check ---------------
# (component, W_sc, expected rows, expected S_t printed on the sheet)
PRINTED = [
    ("INNER_BASEMAT", 609.6, 2, 203.2), ("INNER_BASEMAT", 508.0, 1, 254.0),
    ("OUTER_BASEMAT", 609.6, 2, 203.2), ("OUTER_BASEMAT", 508.0, 1, 254.0),
    ("INTERIOR_FLOOR", 609.6, 2, 203.2), ("INTERIOR_FLOOR", 406.4, 1, 203.2),
]
for comp, wsc, want_rows, want_st in PRINTED:
    band = None
    for wmax, rows_, lineno in sorted(floors.get(comp, [])):
        row = next(r for ln, r in r17 if ln == lineno)
        hit = (wsc < wmax) if row["CMP"].upper() == "LT" else (wsc <= wmax + 1e-4)
        if hit:
            band = (wmax, rows_, row)
            break
    if band is None:
        fail(f"{comp}: no band resolves W_sc = {wsc}")
        continue
    _, rows_, row = band
    check(rows_ == want_rows,
          f"{comp} @ W_sc {wsc}: resolves to {rows_:g} rows, G103 prints {want_rows}")
    div = float(row["S_T_DIVISOR"])
    if div:
        check(abs(wsc / div - want_st) < 0.02,
              f"{comp} @ W_sc {wsc}: derived S_t {wsc/div:.2f} mm != printed {want_st} mm")

# --------------------------------------------------- global constraints
NEEDED = [
    "MIN_STUD_SPACING_19MM", "MIN_STUD_SPACING_12MM", "MIN_EDGE_CLEARANCE",
    "MAX_SPACING_FLOOR_ARRAY", "MAX_SPACING_CONNECTION_ZONE", "MIN_TIE_BAR_SPACING",
    "RADIAL_FIRST_ROW_CLEARANCE", "SPLICE_A_LANDING_PLATE_MAX", "SPLICE_B_SPLICE_LINE_MAX",
    "DENSITY_WINDOW", "DENSITY_MIN_STUDS_DOUBLE_ROW", "DENSITY_MIN_STUDS_SINGLE_ROW",
]
hC, rC = load("global_constraints.csv")
cons = {}
for lineno, row in rC:
    v = num("global_constraints.csv", lineno, row, "VALUE")
    if v is not None:
        cons[row["KEY"].upper()] = v
    check(bool(row.get("SOURCE", "").strip()),
          f"global_constraints.csv:{lineno}: {row['KEY']} has no SOURCE citation")
for k in NEEDED:
    check(k in cons, f"global_constraints.csv: the engine looks up {k}, which is missing")

if "MIN_STUD_SPACING_19MM" in cons:
    check(abs(cons["MIN_STUD_SPACING_19MM"] - 76.2) < 0.01,
          "MIN_STUD_SPACING_19MM should be 76.2 mm per AISC 360 I8.3e")
if "MIN_EDGE_CLEARANCE" in cons:
    check(abs(cons["MIN_EDGE_CLEARANCE"] - 38.1) < 0.01,
          "MIN_EDGE_CLEARANCE should be 38.1 mm per T1-6 n3")

# ------------------------------------------------------------------ report
print(f"rule-table lint: {checks} checks, {len(problems)} problem(s)")
for p in problems:
    print("  PROBLEM  " + p)
sys.exit(1 if problems else 0)
