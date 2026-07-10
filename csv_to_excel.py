import sys
import re
import pandas as pd
from pathlib import Path

def csv_to_excel(csv_path, excel_path=None):
    csv_path = Path(csv_path)

    if not csv_path.exists():
        raise FileNotFoundError(f"CSV file not found: {csv_path}")

    # Default output file name
    if excel_path is None:
        excel_path = csv_path.with_suffix(".xlsx")

    # Read CSV with everything as text
    df = pd.read_csv(
        csv_path,
        dtype=str,
        keep_default_na=False
    )

    # Remove characters illegal in Excel/OpenXML (control chars 0x00-0x1F except \t,\n,\r)
    illegal_xml_pattern = r'[\x00-\x08\x0B\x0C\x0E-\x1F]'
    for col in df.columns:
        # dtype is str because we read with dtype=str; still guard with notna
        if df[col].notna().any():
            df.loc[df[col].notna(), col] = (
                df.loc[df[col].notna(), col]
                .astype(str)
                .str.replace(illegal_xml_pattern, '', regex=True)
            )

    # Write to Excel
    df.to_excel(
        excel_path,
        index=False,
        engine="openpyxl"
    )

    print(f"✅ Converted: {csv_path} → {excel_path}")

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python csv_to_excel.py input.csv [output.xlsx]")
        sys.exit(1)

    input_csv = sys.argv[1]
    output_xlsx = sys.argv[2] if len(sys.argv) > 2 else None

    csv_to_excel(input_csv, output_xlsx)
