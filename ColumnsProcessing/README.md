# Columns CSV Enrichment Tool

A modular Python tool for enriching CSV files with Account Code, Account Description, UOM, MPL, and MPL Description based on `ItemType`.

## Overview

This pipeline mirrors the structure of `PipesProcessing` but focuses on `ItemType`-driven enrichment. The actual mapping logic will be provided and implemented later; the current version lays down the processing skeleton.

## Project Structure

```
ColumnsProcessing/
├── constants.py                 # Column names and mapping dictionaries (incl. MPL map)
├── helpers.py                   # Helper functions (skeleton logic)
├── enrich_csv_standalone.py     # CLI entry to process CSVs
└── README.md                    # This file
```

## Quick Start

```bash
python3 enrich_csv_standalone.py --input sample.csv
```

This will create `sample_enriched.csv` next to your input (unless `--output` is provided).

## Input CSV Requirements

- Must include `ItemType` column
- Optional `ElementId` column: blank values will be skipped if present

## Output Columns

- `ACCOUNT_CODE`
- `ACCOUNT_DESCRIPTION`
- `UOM`
- `MPL`
- `MPL_DESCRIPTION`

`ACCOUNT_CODE` and `UOM` populate from description/code maps; `MPL` and `MPL_DESCRIPTION` derive from `ItemSourceFile` and `mpl_map`.

## Notes

- Encoding: input supports UTF-8 with BOM; output is UTF-8
- No external dependencies
- Designed to be extended with real mapping logic in `helpers.compute_account_from_item_type`


