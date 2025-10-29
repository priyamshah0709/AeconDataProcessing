"""
Helper functions for CSV enrichment.

This module contains utility functions used for extracting and computing
values needed to enrich CSV files with MPL, account codes, and UOM data.
"""

from fractions import Fraction
from pathlib import Path
from typing import Dict, List

from constants import (
    INPUT_ITEM_SOURCE_FILE,
    INPUT_AUTOCAD_COG_Z,
    INPUT_AUTOCAD_SIZE,
    INPUT_AUTOCAD_PLANT_MATERIAL,
    MPL_COLUMN,
    MPL_DESC_COLUMN,
    ACCOUNT_CODE_COLUMN,
    ACCOUNT_CODE_DESC_COLUMN,
    UOM_COLUMN,
    DEFAULT_UOM,
    GROUND_LEVEL_THRESHOLD,
    mpl_map,
    material_map,
    piping_map,
)


def compute_mpl(item_source_file: str) -> str:
    """
    Extract MPL code from ItemSourceFile.

    Args:
        item_source_file: The source file string to parse

    Returns:
        The extracted MPL code or empty string if not found
    """
    if not item_source_file:
        return ""
    parts = item_source_file.split("-")
    if len(parts) < 3:
        return ""
    return parts[2].strip()


def compute_pipe_size_range(size: float | None) -> str | None:
    """
    Determine the pipe size range category.

    Args:
        size: The pipe size in inches

    Returns:
        The size range string or None if size doesn't fit a category
    """
    if size is None:
        return None
    if 2.5 <= size <= 6:
        return "2.5\"-6\""
    if 8 <= size <= 12:
        return "8\"-12\""
    if 14 <= size <= 24:
        return "14\"-24\""
    if 26 <= size <= 40:
        return "26\"-40\""
    if 42 <= size <= 54:
        return "42\"-54\""
    if 60 <= size <= 72:
        return "60\"-72\""
    if 74 <= size <= 90:
        return "74\"-90\""
    if size > 90:
        return ">90\""
    return None


def parse_autocad_size(value):
    """
    Parse AutoCAD size values that may include fractions.

    Handles various formats:
    - Simple decimals: "2.5"
    - Fractions: "3/4"
    - Mixed numbers: "2 1/2"

    Args:
        value: The size value string to parse

    Returns:
        The parsed size as a float or None if parsing fails
    """
    if not value:
        return None

    value = value.replace('"', '').strip()

    # Handle mixed numbers (e.g., "2 1/2")
    if ' ' in value:
        parts = value.split()
        try:
            whole = float(parts[0])
            frac = float(Fraction(parts[1]))
            return whole + frac
        except Exception:
            pass

    # Handle fractions (e.g., "3/4")
    if '/' in value:
        try:
            return float(Fraction(value))
        except Exception:
            pass

    # Handle simple decimals
    try:
        return float(value)
    except ValueError:
        return None


def compute_account_description(row: Dict[str, str]) -> str:
    """
    Compute the account code description based on row data.

    Determines pipe type (above/underground, small/large bore) and material
    to generate the appropriate account description.

    Args:
        row: Dictionary containing row data from CSV

    Returns:
        The computed account code description string
    """
    z_raw = row.get(INPUT_AUTOCAD_COG_Z)
    try:
        z_val = float(z_raw) if z_raw not in (None, "") else 0.0
    except Exception:
        z_val = 0.0

    size_val = parse_autocad_size(row.get(INPUT_AUTOCAD_SIZE))
    material_key = row.get(INPUT_AUTOCAD_PLANT_MATERIAL)
    material_name = material_map.get(material_key)
    if material_name is not None and str(material_name).strip() == "":
        material_name = None

    is_above_ground = z_val > GROUND_LEVEL_THRESHOLD
    is_small_bore = (size_val is not None) and (size_val <= 2)

    if is_above_ground:
        if is_small_bore:
            return (
                "Above Ground Small Bore Pipe (All-In) (0-2\")"
                if material_name is None
                else f"Above Ground Small Bore Pipe (All-In) ({material_name})"
            )
        size_range = compute_pipe_size_range(size_val)
        if size_range is None:
            return "Above Ground Large Bore Pipe"
        return (
            f"Above Ground Large Bore Pipe ({material_name}) ({size_range} Diameter)"
            if material_name is not None
            else "Above Ground Large Bore Pipe"
        )
    else:
        if is_small_bore:
            return (
                "Underground Small Bore Pipe"
                if material_name is None
                else f"Underground Small Bore Pipe - {material_name}"
            )
        size_range = compute_pipe_size_range(size_val)
        if size_range is None:
            return "Underground Large Bore Pipe"
        return (
            f"Underground Large Bore Pipe ({material_name}) ({size_range} Diameter)"
            if material_name is not None
            else "Underground Large Bore Pipe"
        )


def enrich_row(row: Dict[str, str]) -> Dict[str, str]:
    """
    Enrich a single CSV row with computed values.

    Adds MPL, MPL_DESCRIPTION, ACCOUNT_CODE, ACCOUNT_CODE_DESCRIPTION,
    and UOM columns to the row.

    Args:
        row: Dictionary containing the original CSV row data

    Returns:
        Dictionary with original data plus enriched fields
    """
    item_source_file = row.get(INPUT_ITEM_SOURCE_FILE, "")
    mpl_value = compute_mpl(item_source_file)
    mpl_desc_value = mpl_map.get(mpl_value, "")

    account_code_desc_value = compute_account_description(row)
    account_code_value = piping_map.get(account_code_desc_value, "")

    enriched = dict(row)
    enriched[MPL_COLUMN] = mpl_value
    enriched[MPL_DESC_COLUMN] = mpl_desc_value
    enriched[ACCOUNT_CODE_COLUMN] = account_code_value
    enriched[ACCOUNT_CODE_DESC_COLUMN] = account_code_desc_value
    enriched[UOM_COLUMN] = DEFAULT_UOM
    return enriched


def ensure_fieldnames_with_appends(original_fieldnames: List[str]) -> List[str]:
    """
    Ensure all enrichment columns are included in fieldnames.

    Adds any missing enrichment columns to the end of the fieldnames list.

    Args:
        original_fieldnames: The original list of CSV column names

    Returns:
        Complete list of fieldnames including enrichment columns
    """
    fieldnames = list(original_fieldnames)
    for c in [MPL_COLUMN, MPL_DESC_COLUMN, ACCOUNT_CODE_COLUMN, ACCOUNT_CODE_DESC_COLUMN, UOM_COLUMN]:
        if c not in fieldnames:
            fieldnames.append(c)
    return fieldnames


def compute_output_path(input_path: str, explicit_output: str | None = None) -> str:
    """
    Compute the output file path for the enriched CSV.

    If no explicit output path is provided, creates a path by appending
    "_enriched" to the input filename.

    Args:
        input_path: Path to the input CSV file
        explicit_output: Optional explicit output path

    Returns:
        The computed output file path
    """
    if explicit_output:
        return explicit_output
    p = Path(input_path)
    return str(p.with_name(f"{p.stem}_enriched{p.suffix}"))

