# NavisworksPropertyBaker

A Navisworks Manage 2026 add-in that replaces the slow/crashing DataTools CSV import.
It reads the enriched CSVs produced by `PipesProcessing/` and `ColumnsProcessing/`,
matches rows to model elements in one indexed pass, writes the enrichment columns
(`MPL`/`SYSTEM_MPL`, `MPL_DESCRIPTION`, `ACCOUNT_CODE`, `ACCOUNT_CODE_DESCRIPTION`,
`UOM`, `CLEAN_SIZE`, `CLEAN_MATERIAL`) into a static property tab **AECON_DATA**,
and saves the result as NWD.

Unlike DataTools links, these are **static properties baked into the document** the
moment the plugin runs — there is nothing to resolve at publish time, so saving is a
plain file save. You can (and should) open your published **NWD directly**, bake,
and save: the NWF→NWD conversion step is skipped entirely.

The plugin only ever adds/refreshes its own tab. Native properties and other user
tabs (including existing DataTools tabs) are never modified. Re-runs replace the
AECON_DATA values in place — no duplicate tabs, no stale codes.

## Build (Windows machine with Navisworks Manage 2026 + Visual Studio)

1. Open `NavisworksPropertyBaker.sln` in Visual Studio 2022.
2. If Navisworks is not in the default location, edit `<NavisworksDir>` in the
   `.csproj` (default: `C:\Program Files\Autodesk\Navisworks Manage 2026`).
3. Build in **x64** (Debug or Release).
4. Deploy:
   ```powershell
   powershell -ExecutionPolicy Bypass -File deploy\copy-to-plugins.ps1 -Config Release
   ```
   This copies the DLL to
   `%APPDATA%\Autodesk Navisworks Manage 2026\Plugins\NavisworksPropertyBaker\`
   (the folder name must match the DLL name for Navisworks to discover it).
5. Restart Navisworks. The **AECON Property Baker** button appears on the
   *Tool add-ins* ribbon tab.

## First run on a new model — do these in order

1. **Diagnostics** — open the federated model, run the plugin with mode
   *Diagnostics*. It dumps `property_dump_<timestamp>.csv` (property category and
   internal names for ~200 items per source file). Search that file for the rows
   holding your Entity Handle and Element ID values and confirm the
   category/property names. If the internal names differ from the candidates at the
   top of `ModelIndexer.cs`, add them to the front of those lists and rebuild.
2. **Dry run** — mode *Dry run* with your enriched CSV(s). Writes nothing; produces
   `bake_<timestamp>.log` plus `unmatched_<timestamp>.csv` / `duplicates_<timestamp>.csv`.
   Target a **> 99 % match rate** before baking. Common causes of misses:
   - `ItemSourceFile` in the CSV vs. model node names (`.dwg` vs `.nwc`) — the
     fallback matcher handles unambiguous cases automatically; the rest show as
     `AMBIGUOUS`.
   - Rows whose element simply is not in the currently open model (`NOT_IN_MODEL`).
3. **Bake** — mode *Bake*, optionally with a "Save NWD as" path. For ~870K rows
   expect the write phase to take minutes (progress bar shown; cancellable).
4. **Verify** (first time):
   - Select a tagged element → Properties pane shows an **AECON_DATA** tab with
     exact text values (spot-check a hex handle item and a code like `70.12.04.018`).
   - *Find Items* can search on `AECON_DATA / MPL`.
   - Save, close, reopen: the tab persists.
   - Re-run the bake: still exactly one AECON_DATA tab, values refreshed.
   - An element that also has a DataTools tab keeps it untouched.

## Recommended production workflow

1. Publish the NWD as usual (no DataTools links).
2. Open the NWD in Navisworks Manage.
3. Run the baker (Bake mode) with the current enriched CSVs → save.

## Headless batch (optional, same DLL)

```bat
"C:\Program Files\Autodesk\Navisworks Manage 2026\Roamer.exe" ^
  -OpenFile "D:\models\Federated.nwd" ^
  -ExecuteAddInPlugin PropertyBaker.AECON ^
      "csv=D:\out\pipes_enriched.csv" "csv=D:\out\columns_enriched.csv" ^
      "out=D:\out\Federated_baked.nwd" "report=D:\out\reports" "mode=bake" ^
  -NoGui -Exit
```

Exit codes: `0` success, `1` completed with write errors, `2` fatal/bad input,
`3` cancelled.

## CSV expectations

- Comma-delimited, UTF-8 (BOM ok), quoted fields (the pipeline's standard output).
- Exactly one key per row: `EntityHandleValue`/`EntityHandle` (DWG elements) **or**
  `ElementIDValue`/`ElementID` (Revit elements) — rows violating this are skipped,
  same as the Python `should_skip_row` rule.
- `ItemSourceFile` scopes the key (handles are only unique per DWG).
- All values are treated as text end-to-end; hex handles and dotted account codes
  are never numerically mangled.

## Notes

- `SetUserDefined` writes are wrapped in `BeginEdit`/`EndEdit` (the fix for the
  Navisworks 2025+ performance regression). If the interop assembly predates the
  fix the plugin logs a note and continues without it.
- COM property writes have **no undo**. Bake into a copy / save to a new NWD path
  until the workflow is proven.
- Model cleaning (hiding junk lines/labels/dimensions and publishing with
  *Exclude hidden items*) is intentionally out of scope here — see the plan notes.
