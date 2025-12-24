"""
CSV enrichment processing script.

This script processes CSV files to enrich them with MPL, account codes,
and UOM data. It serves as the main entry point for the enrichment process.
"""

import argparse
import csv
import os
from typing import Optional

from helpers import (
    enrich_row,
    ensure_fieldnames_with_appends,
    compute_output_path,
    should_skip_row,
    should_duplicate_row,
    ACCOUNT_CODE_COLUMN,
)


def detect_file_encoding(file_path: str) -> str:
    """
    Attempt to detect the encoding of a CSV file.
    
    Tries multiple common encodings and returns the first one that successfully
    reads the file. This ensures compatibility with various CSV formats.
    
    Args:
        file_path: Path to the CSV file to detect encoding for
        
    Returns:
        The detected encoding name (e.g., 'utf-8-sig', 'utf-8', 'latin1')
    """
    # Common encodings to try, in order of preference
    encodings_to_try = ['utf-8-sig', 'utf-8', 'latin1', 'cp1252', 'iso-8859-1']
    
    for encoding in encodings_to_try:
        try:
            with open(file_path, 'r', encoding=encoding, newline='') as f:
                # Try to read the entire file to ensure encoding is valid
                f.read()
            return encoding
        except (UnicodeDecodeError, LookupError):
            continue
    
    # If all encodings fail, default to utf-8 with error handling
    return 'utf-8'


def enrich_csv(input_csv_path: str, output_csv_path: str) -> None:
    """
    Process and enrich a CSV file with additional computed columns.

    Reads the input CSV with auto-detected encoding, enriches each row with 
    MPL, account codes, and UOM, then writes the enriched data to an 
    Excel-compatible output CSV with UTF-8-sig encoding.

    Args:
        input_csv_path: Path to the input CSV file
        output_csv_path: Path where enriched CSV will be written
    """
    # Detect the encoding of the input file
    detected_encoding = detect_file_encoding(input_csv_path)
    print(f"Detected input encoding: {detected_encoding}")
    
    with open(input_csv_path, "r", encoding=detected_encoding, newline="") as infile:
        reader = csv.DictReader(infile)
        if reader.fieldnames is None:
            raise ValueError("Input CSV has no header row")
        fieldnames = ensure_fieldnames_with_appends(reader.fieldnames)

        # Write with UTF-8-sig (BOM) so Excel preserves special characters like Ø, ñ, etc.
        # This ensures the output can be opened directly in Excel without format conversion
        with open(output_csv_path, "w", encoding="utf-8-sig", newline="") as outfile:
            writer = csv.DictWriter(outfile, fieldnames=fieldnames, extrasaction="ignore")
            writer.writeheader()
            for row in reader:
                if should_skip_row(row, reader.fieldnames):
                    continue

                # Temporary code to copy exisiting rows with ACCOUNT_CODE
                if should_duplicate_row(row):
                    writer.writerow(row)
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
