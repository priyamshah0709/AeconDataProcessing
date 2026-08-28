# StudPlacer — iLogic shear stud placement for BWRX‑300 DP‑SC modules

Places shear studs on an Inventor module assembly directly from the code tables in
**008N9536 Rev 11, sheet G103**, and writes a rule‑annotated stud schedule for QA.

Rules extracted in [../STUD_PLACEMENT_RULES.md](../STUD_PLACEMENT_RULES.md).

```
StudPlacer/
├── rules/          code tables as CSV — edit these when the drawing revises
├── vb/             the engine (3 shared VB files, loaded via AddVbFile)
├── ilogic/         the two rules you load into Inventor
├── tools/          set-install-path.ps1 — retarget the rules after a move
│                   check_rule_tables.py — zero-dependency CSV linter
├── tests/          compiles and runs the real engine; compile-checks the rules
└── samples/        example exclusion file + the full parameter reference
```

**Deploying to a workstation with Inventor:
[INVENTOR_SETUP.md](INVENTOR_SETUP.md).**

---

## The idea in one paragraph

The engine does **not** look up the printed "Transversal Studs Spacings" column. It
derives it: `S_t = W_sc / (lines + 1)`, because *n* stud lines between two diaphragms
create *n+1* gaps. Feed it the tabulated `W_sc` and it reproduces every printed value
in Tables 1‑6 and 1‑7 exactly — and it stays correct at the diaphragm spacings the
table doesn't tabulate, which is most real modules. The printed max‑transversal column
becomes a ceiling check rather than the source of truth. `tools/check_rule_tables.py`
asserts this against the actual CSVs.

---

## Install

Full step-by-step in **[INVENTOR_SETUP.md](INVENTOR_SETUP.md)**. In short:

1. Copy this folder to `C:\Inventor Project\Inventor\StudPlacer\` (a local drive beats a UNC share).
2. Create `parts\Stud_19x157.ipt` — Ø19.1 × 157.2 mm, **axis along the part's own
   +Z, weld face at the part origin**. Placement then reduces to "point local +Z
   along the faceplate normal".
3. Installed elsewhere? Run `tools\set-install-path.ps1 -Root "<new root>"`
   (or `python3 tools/set_install_path.py "<new root>"`) rather than editing by
   hand — the root appears 11 times across the two rule files. `AddVbFile` runs
   before the rule compiles, so its argument must be a literal; it cannot read a
   parameter, which is why it is repeated.
4. **Tools → Options → iLogic Configuration → External Rule Directories** → add
   `C:\Inventor Project\Inventor\StudPlacer\ilogic`.
5. Run **`StudPlacer_SelfTest`** once. It needs no model geometry. Do not use the
   tool until it passes.

## Use

1. Add the `STUD_*` user parameters to the module **assembly** — see
   [samples/module_parameters_example.csv](samples/module_parameters_example.csv).
   Numeric parameters may be unitless (plain mm/deg) or real length/angle parameters;
   the rule normalises both.
2. Optionally point `STUD_EXCLUSIONS_CSV` at a keep‑out file for that module —
   flow holes, sleeves, tie plates, gripper holes. See
   [samples/exclusions_example.csv](samples/exclusions_example.csv).
3. Run **`StudPlacer_Main`**. It writes:
   - `<MODULE_ID>_studs.csv` — the rule‑annotated schedule. Every stud carries the
     code clause that put it there, plus header, notes and a violations block.
   - `<MODULE_ID>_studly.csv` — the legacy `Type,Index,Field,Value` contract from
     `Studly.py`, so anything already consuming that format keeps working.
   - stud occurrences named `STUD_<channel>_<line>_<station>`, grounded.

Re‑running deletes the previous `STUD_*` occurrences first, so the rule is idempotent.
Set `STUD_PLACE_GEOMETRY = False` to get the CSVs without touching the model.

---

## What the engine implements

| Rule | Source | Where |
|---|---|---|
| Wall arrays: 1 or 2 stud columns, `S_t = W_sc/(n+1)`, `S_l` per elevation band | T1‑6 | `rules/table_1_6_walls.csv` |
| Floor/basemat arrays degrading 2 rows → 1 row → **no studs** as `W_sc` narrows | T1‑7 | `rules/table_1_7_floors.csv` |
| Min spacing 76.2 mm (3/4″) / 50.8 mm (1/2″), by stud diameter | T1‑6 n1, G109 n1 | `StudConstraints.MinSpacingFor` |
| 38.1 mm clearance from **stud base edge** to any free edge, hole or sleeve | T1‑6 n3, T1‑7 n5/n10 | `EdgeClear` + `ApplyExclusions` |
| Max transversal ceiling (381 / 406.4 / 431.8 mm walls, 254 mm floors) | T1‑6, T1‑7 n2 | `Validate` |
| Termination: `≤ S_l` at a landing/cover plate, `≤ S_l/2` at a bare splice | Detail 2 n1, Detail 3 n1–n2 | `TerminationAllowance` |
| Splice A 86.9 mm / Splice B 76.2 mm allowances | Detail 3 n1–n2 | same, opt‑in via `STUD_SPLICE_ID_*` |
| Staggered arrays measured on the **shortest straight line** between centres | Detail 2 n3 | `Validate`, bucketed nearest‑neighbour |
| Radial diaphragms: hold 152.4 mm off the diaphragm, squeeze the middle | T1‑7 n3 | `STUD_TRANSVERSE_MODE = RADIAL_CLEARANCE` |
| Density make‑up: ≥8 studs/305 mm (double row) or ≥4 (single row) after exclusions | T1‑7 n7/n8 | `DensityMakeup` + greedy back‑fill |
| `W_sc` exceeding the maximum permitted diaphragm spacing | T1‑6, T1‑7 | reported as `WSC_EXCEEDS_TABLE_MAX` |

### The radial floor case

Worth calling out because `Studly.py` got it wrong. On a ring floor the diaphragms are
**radial**, so the cavity width `w = r·θ` grows with radius. The engine therefore
re‑resolves the Table 1‑7 band at **every radial station**, which reproduces the real
behaviour: a 3000–17000 mm ring at a 2° diaphragm pitch comes out as

```
r < 4366 mm          no studs        (cavity below 152.4 mm)
4366 – 11640 mm      1 stud row
11640 – 17000 mm     2 stud rows
```

and it refuses to place anything where the cavity outgrows the maximum tabulated
diaphragm spacing — which is the engine telling you that stretch needs another diaphragm.

---

## What is NOT implemented yet

Deliberate scope line, not oversights:

- **Table 1‑11** (wing / stair / elevator / partition SC walls) and the Detail J/K
  periphery ramp‑in arrays. Different stud sizes and tie‑bar interaction.
- **Connection‑zone per‑metre densities** (20 / 30 / 32 / 33 / 40 studs per metre) and
  the proportional adjustment when flow‑hole spacing deviates from the 1 m unit length.
- **1/2″ cover‑plate rows** — door surrounds (50.8 / 304.8 mm) and floor cover plates
  (51 / 268 mm).
- **Airlock / CRD port `S_variable` row‑count rules.** The constants are already in
  `global_constraints.csv`; nothing consumes them yet.
- **Reading geometry from the model.** Phase 1 is parameter‑driven. Diaphragm positions,
  flow holes and sleeves come from parameters and the exclusion CSV, not from the solid.

---

## Verification

```bash
tests/run-tests.sh        # macOS / Linux
tests\run-tests.cmd       # Windows   (needs: winget install Microsoft.DotNet.SDK.8)
```

Three stages, all green:

| Stage | What it does |
|---|---|
| `tools/check_rule_tables.py` | **141 checks.** Lints the CSVs: required columns, locale-safe numbers, `S_T_DIVISOR == lines + 1` on every row, derived `S_t` matches the value *printed* on G103, floor bands ordered with no gap in `W_sc` coverage, elevation bands contiguous, every constraint the engine looks up exists. Needs nothing but Python — run it after transcribing a new revision. |
| `tests/ilogic-syntax/` | Compile-checks both `.iLogicVb` files the way iLogic will: strips `AddVbFile`, builds against the engine plus stubbed iLogic globals. Also enforces iLogic's **program-format rule** — a rule that declares any `Sub`/`Function` must have an explicit `Sub Main()` first — which a plain VB compiler accepts but the Inventor rule editor rejects. Catches syntax, bad API member names and format errors *before* the file reaches Inventor. |
| `tests/` | **132 assertions.** Compiles the actual `vb/*.vb` files that Inventor loads and runs them: every table band, end-to-end flat / curved / radial builds, exclusion geometry, density make-up, matrix orthonormality and the mm→cm conversion, idempotent re-runs, the occurrence guard, CSV round-trips, and locale safety under a comma-decimal culture. |

`tests/InventorStubs.vb` exists only so the code can be type-checked off Windows;
it is never deployed. It is deliberately faithful on the point that matters —
`Inventor.Document` exposes `DocumentType` and `UnitsOfMeasure` but **not**
`ComponentDefinition`, exactly as the real API does.

### What compiling actually caught

Three bugs that reading the code did not:

1. **`SignedDistance(u, v)` shadowed the `U`/`V` fields.** VB identifiers are
   case-insensitive, so `u - U` silently evaluated to zero and every keep-out zone
   reported "this stud is dead centre inside me" — deleting the **entire array**
   whenever any flow hole, sleeve or tie plate was defined. Parameters are now
   `pu`/`pv`.
2. **`Inventor.Document` has no `ComponentDefinition` member.** The parameter
   helpers took the base `Document` type, which does not compile against the real
   API. They now take `AssemblyDocument`.
3. **`mE` is the `Me` keyword.** A test-local variable; harmless, but only a
   compiler finds it.

And one that only running it inside Inventor caught, now guarded against:

4. **iLogic requires an explicit `Sub Main()`** as soon as a rule declares any
   `Sub` or `Function` of its own — it stops auto-wrapping loose statements.
   A plain VB compiler builds such a rule happily; the Inventor rule editor
   rejects it with *"Error in rule program format"*. `wrap_rules.py` now asserts
   the entry point exists and comes first, so stage 2 fails the build instead.

## Known caveats — read before trusting output

1. **It has not been run inside Inventor.** The engine is compiled and tested (see
   *Verification* above) and the rule files are compile‑checked against faithful API
   stubs, but no one has yet clicked *Run Rule* on a real module. The stubs model the
   API surface the rules touch, not Inventor's runtime behaviour. **Run
   `StudPlacer_SelfTest` first**, then a real module with
   `STUD_PLACE_GEOMETRY = False`, and read the violations block before placing
   geometry.

2. **Splice A / Splice B are an interpretation.** Detail 3 says a maximum 86.9 mm
   (Splice A, basemat OSW) and 76.2 mm (Splice B, pedestal wall) "is acceptable". Read
   literally that is *tighter* than `S_l` in some bands and *looser* in others. The
   engine treats them as explicit overrides you opt into per module via
   `STUD_SPLICE_ID_*`, and cites the source in the schedule so the reviewer sees it.
   Confirm the intent with the responsible engineer before relying on it.

3. **The 38.1 mm edge clearance is applied at free edges, cover plates and sleeves
   — not at splice lines or landing plates.** The notes word it against a *free edge*,
   a flow hole or a sleeve (T1‑6 n3 / T1‑7 n5), and T1‑7 n10 extends it to a cover
   plate. A splice is a continuous welded joint, not a free edge. This is load‑bearing:
   at a bare splice Detail 2/3 puts the stud centre within `S_l/2`, which on an interior
   floor is 38.1 mm — already inside the centre‑to‑boundary form of the edge rule. Read
   the other way, the interior floor array has no feasible solution at all. Confirm with
   the responsible engineer.

4. **Interior floors have zero pitch slack.** `S_l` is 76.2 mm and the AISC minimum
   spacing is *also* 76.2 mm, so the pitch must land on it exactly. `Stations()` solves
   for a pitch inside `[min spacing, S_l]` with the remainder split between the two ends
   rather than absorbed into the pitch — the naive approach silently produces a
   non‑compliant array. `parity_check.py` asserts the lower bound, not just the ceiling.

5. **Occurrence count.** `STUD_MAX_OCCURRENCES` defaults to 4000 and placement is
   skipped above it — the CSVs are still written. Inventor gets sluggish well before
   the hard limit; consider Model States for the heavy modules.

6. **Both source drawings are HOLD / "Deferred Verification".** Tables 1‑6, 1‑7 and
   Detail 2 all carry Rev 11 revision clouds. When Rev 12 lands, edit the CSVs in
   `rules/` and re‑run `tests/run-tests.sh` (or at minimum
   `python tools/check_rule_tables.py`) plus `StudPlacer_SelfTest`. No code change.

7. **The 254 mm vs 610 mm maximum is scoped, not global.** 254 mm is the floor/basemat
   array ceiling (T1‑7 n2); 610 mm applies in wall connection zones (G105/G106/G108),
   which this tool does not generate. Don't widen one into the other.

---

## Relationship to `Studly.py`

Kept: the `Type,Index,Field,Value` CSV contract (`Assembly_Type` 1–5, `Roll` as
degrees × 10), the flat / curved‑inner / curved‑outer / annular geometry families, and
the `Datum Alignment` concept — which turns out to be exactly Table 1‑6's
`S_t = W_sc/2` vs `W_sc/3` distinction.

Dropped: the tkinter GUI (re‑keying 12 numbers per module doesn't scale) and the
PythonOCC STEP writer (conda‑only dependency; Inventor makes the geometry natively).

Added: all of it — Studly had no code rules at all. Omission was manual `(col,row)`
rectangles typed into a form.
