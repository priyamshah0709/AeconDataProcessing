"""
CSV enrichment processing script.

This script processes CSV files to enrich them with MPL, account codes,
and UOM data. It serves as the main entry point for the enrichment process.
"""

import argparse
import csv
import os
from pathlib import Path
from typing import Optional

try:
    import pandas as pd
    PANDAS_AVAILABLE = True
except ImportError:
    PANDAS_AVAILABLE = False

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


def detect_csv_delimiter(file_path: str, encoding: str) -> str:
    """
    Detect the delimiter used in a CSV file by analyzing the header row.
    
    Determines the delimiter by counting occurrences of common delimiters
    in the first row (column names). The delimiter with the most occurrences
    is selected.
    
    Args:
        file_path: Path to the CSV file
        encoding: Encoding to use when reading the file
        
    Returns:
        The detected delimiter character (e.g., ',', ';', '\t', '|')
    """
    try:
        with open(file_path, 'r', encoding=encoding, newline='') as f:
            # Read only the first line (header with column names)
            header = f.readline()
            
            if not header:
                return ','
            
            # Common delimiters to test, in order of preference
            potential_delimiters = [';', ',', '\t', '|']
            
            # Count occurrences of each delimiter in the header row
            delimiter_counts = {}
            for delim in potential_delimiters:
                count = header.count(delim)
                if count > 0:
                    delimiter_counts[delim] = count
            
            # If no delimiters found, default to comma
            if not delimiter_counts:
                return ','
            
            # Return the delimiter with the highest count
            return max(delimiter_counts, key=delimiter_counts.get)
            
    except Exception:
        # If detection fails, default to comma
        return ','


def convert_csv_to_excel(csv_path: str, excel_path: str) -> None:
    """
    Convert CSV to Excel format with all data as text to prevent Excel auto-formatting.
    
    Uses pandas to read CSV with dtype=str to force all columns as text, then writes
    to Excel format. This prevents Excel from interpreting values starting with -, =, +, @
    as formulas or auto-converting data types.
    
    Args:
        csv_path: Path to the input CSV file (enriched CSV with comma delimiter)
        excel_path: Path where Excel file will be written
    """
    if not PANDAS_AVAILABLE:
        print("Warning: pandas not installed. Cannot generate Excel output.")
        print("To install: pip install pandas openpyxl")
        return
    
    try:
        # Read CSV with all columns as strings to prevent any type interpretation
        # Enriched CSV always uses comma delimiter
        df = pd.read_csv(
            csv_path, 
            dtype=str, 
            encoding='utf-8-sig',
            delimiter=',',  # Enriched CSV always uses comma
            keep_default_na=False  # Prevent empty strings from becoming NaN
        )
        
        # Write to Excel - all data is stored as text
        df.to_excel(excel_path, index=False, engine='openpyxl')
        
    except Exception as e:
        print(f"Warning: Failed to create Excel file: {str(e)}")
        print("CSV output is still available.")


def enrich_csv(input_csv_path: str, output_csv_path: str, output_excel_path: Optional[str] = None) -> None:
    """
    Process and enrich a CSV file with additional computed columns.

    Reads the input CSV with auto-detected encoding and delimiter, enriches 
    each row with MPL, account codes, and UOM, then writes the enriched data 
    to CSV and optionally Excel format.

    Args:
        input_csv_path: Path to the input CSV file
        output_csv_path: Path where enriched CSV will be written
        output_excel_path: Optional path where Excel file will be written
    """
    # Detect the encoding of the input file
    detected_encoding = detect_file_encoding(input_csv_path)
    print(f"Detected input encoding: {detected_encoding}")
    
    # Detect the delimiter (comma, semicolon, tab, etc.)
    detected_delimiter = detect_csv_delimiter(input_csv_path, detected_encoding)
    delimiter_name = {',' : 'comma', ';': 'semicolon', '\t': 'tab'}.get(detected_delimiter, repr(detected_delimiter))
    print(f"Detected input delimiter: {delimiter_name} ({repr(detected_delimiter)})")
    
    with open(input_csv_path, "r", encoding=detected_encoding, newline="") as infile:
        reader = csv.DictReader(
            infile, 
            delimiter=detected_delimiter,
            quoting=csv.QUOTE_MINIMAL,
            doublequote=True,
            skipinitialspace=True
        )
        if reader.fieldnames is None:
            raise ValueError("Input CSV has no header row")
        fieldnames = ensure_fieldnames_with_appends(reader.fieldnames)

        # Write CSV with UTF-8-sig (BOM), comma delimiter, and QUOTE_ALL
        # Always use comma for output CSV for maximum compatibility
        with open(output_csv_path, "w", encoding="utf-8-sig", newline="") as outfile:
            writer = csv.DictWriter(
                outfile,
                fieldnames=fieldnames,
                delimiter=',',  # Always use comma for output
                quoting=csv.QUOTE_ALL,
                extrasaction="ignore"
            )
            writer.writeheader()
            
            row_number = 1  # Track row numbers for debugging
            for row in reader:
                row_number += 1
                try:
                    if should_skip_row(row, reader.fieldnames):
                        continue

                    # Temporary code to copy exisiting rows with ACCOUNT_CODE
                    if should_duplicate_row(row):
                        writer.writerow(row)
                        continue

                    enriched = enrich_row(row)
                    writer.writerow(enriched)
                except Exception as e:
                    print(f"Warning: Error processing row {row_number}: {str(e)}")
                    print(f"Skipping row and continuing...")
                    continue
    
    # Convert to Excel if requested (prevents Excel auto-formatting issues)
    if output_excel_path:
        print(f"Converting to Excel format...")
        convert_csv_to_excel(output_csv_path, output_excel_path)

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
    parser.add_argument(
        "--excel",
        action="store_true",
        help="Generate Excel (.xlsx) output with all data as text (prevents formula interpretation and data loss)",
    )
    args = parser.parse_args()

    input_path = os.path.abspath(args.input)
    output_path = os.path.abspath(compute_output_path(input_path, args.output))
    
    # Generate Excel path if requested
    excel_path = None
    if args.excel:
        p = Path(output_path)
        excel_path = str(p.with_suffix('.xlsx'))

    enrich_csv(input_path, output_path, excel_path)
    print(f"Wrote enriched CSV: {output_path}")
    
    if excel_path and PANDAS_AVAILABLE:
        print(f"Wrote enriched Excel: {excel_path}")
    elif args.excel and not PANDAS_AVAILABLE:
        print(f"Note: Install pandas and openpyxl to generate Excel files:")
        print(f"      pip install pandas openpyxl")


if __name__ == "__main__":
    main()
