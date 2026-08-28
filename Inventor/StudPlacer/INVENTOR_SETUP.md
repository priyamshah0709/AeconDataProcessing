# Running StudPlacer on a workstation with Inventor

Step‑by‑step for a Windows machine with Autodesk Inventor installed. Nothing here
needs a developer toolchain — the .NET SDK is only for re‑running the test suite
(§8), which is optional.

Tested against the Inventor 2024/2025 iLogic API surface. Nothing used here is
version‑specific.

---

## 1. Deploy the folder

Copy the whole `StudPlacer` folder to a **local** path on the workstation:

```
C:\Inventor Project\Inventor\StudPlacer\
    rules\      code tables  (CSV)
    vb\         the engine   (3 .vb files)
    ilogic\     the 2 rules you load into Inventor
    samples\    example exclusion + parameter files
    parts\      you create this in step 2
```

**Use a local drive. Not OneDrive, not a mapped share.**

- **OneDrive / SharePoint‑synced folders are the main trap.** Files there can be
  online‑only placeholders that look present in Explorer but cannot be read by
  iLogic, and `AddVbFile` resolves at compile time with no useful error. If you
  must keep a copy in OneDrive, work from a local copy and right‑click the
  folder → *Always keep on this device* for the synced one.
- **UNC shares** work but resolve slowly and fail confusingly when the share is
  unavailable. If you need one, use a full UNC path
  (`\\server\eng\StudPlacer\vb\StudRules.vb`), never a mapped drive letter that
  may not exist on the next machine.
- Spaces in the path are fine — `C:\Inventor Project\Inventor\StudPlacer` is a
  supported install root.

If the folder arrived as a downloaded `.zip`, **unblock it before extracting** —
right‑click the zip → Properties → tick *Unblock* → OK. Windows marks files from
another machine, and the VB compiler iLogic uses will refuse them otherwise.

## 2. Create the stud part

New Part (`.ipt`), millimetres:

1. Sketch on the **XY plane**, circle centred on the **origin**, Ø **19.1 mm**.
2. Extrude **157.2 mm** in **+Z**.
3. Save as `C:\Inventor Project\Inventor\StudPlacer\parts\Stud_19x157.ipt`.

Two rules matter and nothing else does:

- **the stud axis runs along the part's own +Z**
- **the weld face (base) sits at the part origin**

Placement then reduces to "point local +Z along the faceplate normal". Model the
head, the fillet, whatever the supplier drawing shows — just keep the axis and
the base where they are. A second part will be needed for the 12.7 × 101.6 mm
cover‑plate studs when that scope lands.

## 3. Point the rules at your install path

Open both files in `ilogic\` in Notepad. If you used `C:\Inventor Project\Inventor\StudPlacer`,
change nothing. Otherwise edit **two things**:

- the three `AddVbFile` lines at the top of each file
- the `STUD_RULES_DIR` default in `StudPlacer_Main.iLogicVb`

`AddVbFile` is a pre‑compile directive, so its argument has to be a literal
string — it cannot read a parameter. That's why the path appears twice.

> **If you edit a rule file:** keep the `AddVbFile` lines at the very top, the
> whole body inside `Sub Main()` … `End Sub`, and the helper `Sub`/`Function`
> declarations after `End Sub`. iLogic only auto-wraps loose statements while a
> rule declares no subroutines of its own; the moment it does, an explicit
> `Sub Main()` becomes mandatory.

### Verify the install before going further

Run the same script with the root you used — it rewrites nothing when the path
already matches, and reports which of the nine expected files are present:

```powershell
.\tools\set-install-path.ps1 -Root "C:\Inventor Project\Inventor\StudPlacer"
```

`parts\Stud_19x157.ipt` will read MISSING until you do step 2. Everything else
should say OK — the rules fail at run time otherwise, and the most common cause
is copying `ilogic\` and `vb\` but forgetting `rules\`.

## 4. Register the rules with iLogic

In Inventor: **Tools → Options → iLogic Configuration → External Rule
Directories** → add `C:\Inventor Project\Inventor\StudPlacer\ilogic` → OK.

Then open the iLogic browser (**Manage → iLogic → iLogic Browser**) and pick the
**External Rules** tab. Both rules should be listed. If they are not, right‑click
in the tab → *Add External Rule* and browse to the `.iLogicVb` files.

> Prefer external rules over rules embedded in each document. One copy, one place
> to update when the drawing revises.

## 5. Run the self‑test — before anything else

In the External Rules tab, right‑click **StudPlacer_SelfTest** → **Run Rule**.

It needs no model geometry, so any open document will do. It asserts the engine
reproduces the values printed on G103 Tables 1‑6 and 1‑7, and reports
`x passed, y failed`.

**Do not use the tool until this passes.** If it fails, the message names each
failing assertion; the usual cause is an edited rule CSV.

## 6. Set up a module assembly

Open the module **assembly** (not a part) and add the `STUD_*` user parameters:
**Manage → Parameters → Add (User Parameters)**.

The full list with examples is in
[samples/module_parameters_example.csv](samples/module_parameters_example.csv).
A minimal flat SCCV wall needs:

| Parameter | Unit Type | Example |
|---|---|---|
| `STUD_COMPONENT` | Text | `SCCV_WALL` |
| `STUD_GEOMETRY` | Text | `FLAT` |
| `STUD_ELEV_M` | ul | `-30.5` |
| `STUD_WSC_MM` | ul | `609.6` |
| `STUD_PLATE_WIDTH_MM` | ul | `2438.4` |
| `STUD_PLATE_LENGTH_MM` | ul | `3600` |
| `STUD_TERM_START` | Text | `LANDING_PLATE` |
| `STUD_TERM_END` | Text | `SPLICE` |
| `STUD_PART_PATH` | Text | `C:\Inventor Project\Inventor\StudPlacer\parts\Stud_19x157.ipt` |

To make a **Text** parameter: Add a user parameter, then change its *Unit Type*
column to `Text`. For numeric ones, `ul` (unitless) holding plain millimetres or
degrees is simplest. Real `mm`/`deg` parameters also work — the rule detects the
unit type and normalises either form, so you can drive `STUD_WSC_MM` off a model
dimension if you prefer.

Optionally point `STUD_EXCLUSIONS_CSV` at a keep‑out file for the module (flow
holes, sleeves, tie plates, gripper holes) — see
[samples/exclusions_example.csv](samples/exclusions_example.csv).

## 7. Run it

With the module assembly active: right‑click **StudPlacer_Main** → **Run Rule**.

You get a summary dialog and three outputs, written next to the assembly unless
`STUD_OUTPUT_DIR` says otherwise:

| Output | What it's for |
|---|---|
| `<MODULE_ID>_studs.csv` | The audit artefact. Header, notes, violations, then one row per stud carrying the code clause that placed it. |
| `<MODULE_ID>_studly.csv` | The legacy `Type,Index,Field,Value` contract, so anything already consuming Studly output keeps working. |
| Stud occurrences | Named `STUD_<channel>_<line>_<station>`, grounded. |

**Dry run first.** Set `STUD_PLACE_GEOMETRY = False` to get the CSVs without
touching the model. Read the violations block, then set it back to `True`.

Re‑running deletes the previous `STUD_*` occurrences before placing, so the rule
is idempotent — change a parameter and run again.

## 8. Optional: re‑run the full test suite on Windows

Only needed if you edit the engine or the rule tables.

```powershell
winget install Microsoft.DotNet.SDK.8
cd C:\Inventor Project\Inventor\StudPlacer
tests\run-tests.cmd
```

That lints the rule CSVs, compile‑checks both `.iLogicVb` files the way iLogic
will, then compiles and runs the engine regression suite. The linter alone
(`python tools\check_rule_tables.py`) needs nothing but Python and is the right
check after transcribing a new drawing revision.

---

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `'File' is ambiguous, imported from the namespaces or types 'System.IO, Inventor'` | A `.vb` engine file uses a bare `File.` / `Path.` / `Directory.`. iLogic compiles AddVbFile sources under its own global imports, and the Inventor API has its own `File` and `Path` types. Write `System.IO.File` / `System.IO.Path` in full. The shipped files already do; if you see this, one was edited. `tests/run-tests.sh` catches it. |
| `Error in rule program format: The rule must contain: Sub Main() ... End Sub` | The rule lost its `Sub Main()` wrapper. iLogic auto-wraps loose statements ONLY while a rule declares no `Sub`/`Function` of its own; both of these rules declare helpers, so both need an explicit `Sub Main()` with the helpers *after* `End Sub`. Both shipped files already have it — if you see this, the file was edited. `tests/ilogic-syntax/wrap_rules.py` checks for it. |
| `Error on line 1: file not found` or `AddVbFile` failure | The path in the `AddVbFile` lines does not exist on this machine. Fix step 3. Check the zip was unblocked (step 1). |
| `StudPlacer code tables not found` | The `rules\` folder is missing or incomplete — copying `ilogic\` and `vb\` but not `rules\` is the usual cause. The message names the folder searched, which files are absent, and where to put them. Run `tools\set-install-path.ps1 -Root "<root>"` to check the whole layout at once. The message also searches nearby folders — if the tables are sitting somewhere else it names that path, and you can either move them or point `STUD_RULES_DIR` at it. |
| `Parameter STUD_COMPONENT is required` | The parameter is missing or misspelled, or it was added to a **part** rather than the assembly. |
| `No Table 1-6 band for component 'X' at elevation Y` | `STUD_COMPONENT` is not one of the six recognised values, or `STUD_ELEV_M` is outside the elevation bands on G103. |
| `STUD_WSC_MM must be > 0 for FLAT geometry` | A required parameter is missing, so it defaulted to 0. Compare against the sample parameter list. |
| Dimensions come out 10× too large or small | A numeric parameter was created with a length unit while holding a value meant as unitless, or vice versa. Either is supported — but the number has to match the unit. |
| `StudPlacer must be run from the module ASSEMBLY` | A part or drawing is active. |
| `Placement skipped: N studs exceeds the 4000 occurrence guard` | Working as intended. The CSVs were still written. Raise `STUD_MAX_OCCURRENCES` only if you have thought about the file size, or split the module. |
| Inventor becomes sluggish after placing | Thousands of occurrences. Use Model States to suppress the stud sub‑assembly for normal work, or run with `STUD_PLACE_GEOMETRY = False` and consume the CSV downstream. |
| Rule appears to hang | Placement disables screen updating on purpose. It restores it in a `Finally` block even on error. Give a large module a minute. |
| `WSC_EXCEEDS_TABLE_MAX` in the violations block | Not a tool bug — the diaphragm spacing exceeds what G103 permits for that component. On a radial floor it means that stretch needs another diaphragm. |
| Self‑test fails after editing a rule CSV | Run `python tools\check_rule_tables.py` — it names the offending row and column. |

## What this does not do yet

Table 1‑11 (wing / stair / elevator / partition SC walls), the connection‑zone
per‑metre densities, the ½″ cover‑plate rows, and the airlock `S_variable`
row‑count rules are not implemented. Neither is reading diaphragm positions and
openings from the solid — those come from parameters and the exclusion CSV.
See [README.md](README.md) for the full scope line.
