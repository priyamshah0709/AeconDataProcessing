"""
CSV enrichment processing script.

This script processes CSV files to enrich them with MPL, account codes,
and UOM data. It serves as the main entry point for the enrichment process.
"""

import argparse
import csv
import os

from helpers import (
    enrich_row,
    ensure_fieldnames_with_appends,
    compute_output_path,
    should_skip_row,
)


def enrich_csv(input_csv_path: str, output_csv_path: str) -> None:
    """
    Process and enrich a CSV file with additional computed columns.

    Reads the input CSV, enriches each row with MPL, account codes, and UOM,
    then writes the enriched data to the output CSV.

    Args:
        input_csv_path: Path to the input CSV file
        output_csv_path: Path where enriched CSV will be written
    """
    with open(input_csv_path, "r", encoding="utf-8-sig", newline="") as infile:
        reader = csv.DictReader(infile)
        if reader.fieldnames is None:
            raise ValueError("Input CSV has no header row")
        fieldnames = ensure_fieldnames_with_appends(reader.fieldnames)

        with open(output_csv_path, "w", encoding="utf-8", newline="") as outfile:
            writer = csv.DictWriter(outfile, fieldnames=fieldnames, extrasaction="ignore")
            writer.writeheader()
            for row in reader:
                if should_skip_row(row, reader.fieldnames):
                    continue
                enriched = enrich_row(row)
                writer.writerow(enriched)


def main() -> None:
    """
    Main entry point for the CSV enrichment script.

    Parses command-line arguments and orchestrates the enrichment process.
    """
    parser = argparse.ArgumentParser(description="Enrich CSV with MPL, Account Codes, and UOM (standalone)")
    parser.add_argument("--input", required=True, help="Path to input CSV (e.g., TestSample.csv)")
    parser.add_argument(
        "--output",
        required=False,
        help="Optional explicit output CSV path; defaults to <input>_enriched.csv",
    )
    args = parser.parse_args()

    input_path = os.path.abspath(args.input)
    output_path = os.path.abspath(compute_output_path(input_path, args.output))

    enrich_csv(input_path, output_path)
    print(f"Wrote enriched CSV: {output_path}")


if __name__ == "__main__":
    main()
