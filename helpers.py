"""
Helper functions for CSV enrichment.

This module contains utility functions used for extracting and computing
values needed to enrich CSV files with MPL, account codes, and UOM data.
"""

import re
from fractions import Fraction
from pathlib import Path
from typing import Dict, List

from constants import (
    INPUT_ITEM_SOURCE_FILE,
    INPUT_AUTOCAD_COG_Z,
    INPUT_AUTOCAD_SIZE,
    INPUT_AUTOCAD_PLANT_MATERIAL,
    INPUT_ENTITY_HANDLE,
    MATERIAL_CODE_COLUMN,
    ITEM_MATERIAL_COLUMN,
    ITEM_TYPE_COLUMN,
    INPUT_ITEM_TYPE,
    INPUT_CIVIL3D_INFO,
    MPL_COLUMN,
    MPL_DESC_COLUMN,
    ACCOUNT_CODE_COLUMN,
    ACCOUNT_CODE_DESC_COLUMN,
    UOM_COLUMN,
    DEFAULT_UOM,
    GROUND_LEVEL_THRESHOLD,
    mpl_map,
    material_map,
    material_codes_map,
    ItemMaterial_PlantMaterial_map,
    material_keys_list,
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
    # Check if this is a Pressure Pipe - if so, set default values
    ItemType = row.get(INPUT_ITEM_TYPE)
    ItemSourceFile = row.get(INPUT_ITEM_SOURCE_FILE)
    ItemSourceFileCode = compute_mpl(ItemSourceFile)
    
    if ItemType == "Pressure Pipe" or ItemSourceFileCode in ["CUW"]:
        # Set default values for Pressure Pipe
        row[INPUT_AUTOCAD_COG_Z] = float('-inf')
        row[INPUT_AUTOCAD_SIZE] = compute_size_from_civil3dInfo(row.get(INPUT_CIVIL3D_INFO))
        row[INPUT_AUTOCAD_PLANT_MATERIAL] = compute_material_from_civil3dInfo(row.get(INPUT_CIVIL3D_INFO))
    
    missing_values = []
    
    # Step 1: Check material first
    material_key = row.get(INPUT_AUTOCAD_PLANT_MATERIAL)
    material_name = None
    if material_key in (None, ""):
        # Fallback: try to infer from material code column
        inferred_key = compute_material_key(row.get(MATERIAL_CODE_COLUMN), row.get(ITEM_MATERIAL_COLUMN), row.get(ITEM_TYPE_COLUMN))
        if inferred_key:
            material_key = inferred_key
    
    if material_key not in (None, ""):
        material_name = material_map.get(material_key)
        if material_name is not None and str(material_name).strip() == "":
            material_name = None
    
    if material_name is None:
        missing_values.append("Material")
    
    # Step 2: Check size
    size_raw = row.get(INPUT_AUTOCAD_SIZE)
    size_val = None
    if size_raw in (None, ""):
        missing_values.append("Size")
    else:
        size_val = parse_autocad_size(size_raw)
        if size_val is None:
            missing_values.append("Size")
    
    # Step 3: Check COG_Z
    z_raw = row.get(INPUT_AUTOCAD_COG_Z)
    z_val = None
    if z_raw in (None, ""):
        missing_values.append("COG_Z")
    else:
        try:
            z_val = float(z_raw)
        except Exception:
            missing_values.append("COG_Z")
    
    # If COG_Z or Size are missing, report them all
    if len(missing_values) > 0 and ("COG_Z" in missing_values or "Size" in missing_values):
        return f"Missing values: {', '.join(missing_values)}"
    
    # All values are present, proceed with normal logic
    is_above_ground = z_val > GROUND_LEVEL_THRESHOLD
    is_small_bore = size_val <= 2

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

def compute_material_key(material_code_value: str, item_material_value: str, item_type_value: str) -> str:
    """
    Infer the material key (e.g., "SS", "CS", "PVC", "Alloy") from a
    material code string by matching exactly against any value contained in
    material_codes_map. Matching is case-insensitive and trims whitespace.

    Resolution order:
    1) Match material_code_value against material_codes_map values
    2) Map item_material_value using ItemMaterial_PlantMaterial_map
    3) If still unknown, check item_type_value for any substring from
       material_keys_list (case-insensitive) and return that key.

    Returns the material key if found, otherwise an empty string.
    """
    # Try to derive material key from material code
    if material_code_value:
        needle = str(material_code_value).strip().upper()
        for key, code_values in material_codes_map.items():
            for candidate in code_values:
                if needle == str(candidate).strip().upper():
                    return key
                
    # Try to derive material key from item material
    if item_material_value:
        mapped = ItemMaterial_PlantMaterial_map.get(item_material_value)
        if mapped:
            return mapped

    # 3) Inspect item_type_value for any material key substring
    if item_type_value:
        up = str(item_type_value).upper()
        for key in material_keys_list:
            if str(key).upper() in up:
                return key

    # No material key found
    return ""

def compute_size_from_civil3dInfo(civil3dInfo: str) -> str:
    """
    Extract pipe size from Civil3D information and convert to inches.

    Behavior:
    - Prefer explicit inch values inside parentheses, e.g. "(4\")" -> "4\""
    - Otherwise, extract the first occurrence of a number followed by "mm"
      (supports forms like "315mmØ" or "300mm") and convert to inches.
    - Round to the nearest 0.5 inch and return as a string like "3\"" or "1.5\"".
    - If nothing can be parsed, default to "2\"".
    """
    if not civil3dInfo:
        return ""

    text = civil3dInfo.strip()

    # 1) Prefer explicit inches in parentheses, e.g. (4")
    match_in_parentheses = re.search(r"\((\d+(?:\.\d+)?)\s*\"?\)", text)
    if match_in_parentheses:
        inches_val = float(match_in_parentheses.group(1))
        return f"{inches_val:g}\""

    # 2) Normalize common variants like "mmØ" -> "mm"
    normalized = text.replace("mmØ", "mm").replace("Ø", "")

    # 3) Extract first number followed by mm
    match_mm = re.search(r"(\d+(?:\.\d+)?)\s*mm", normalized, flags=re.IGNORECASE)
    if match_mm:
        mm_val = float(match_mm.group(1))
        inches = mm_val / 25.4
        # Round to nearest 0.5 inch
        rounded = round(inches * 2) / 2.0
        if abs(rounded - round(rounded)) < 1e-9:
            return f"{int(round(rounded))}\""
        return f"{rounded:.1f}\""

    # 4) Fallback: look for a bare inches number with a quote not in parentheses
    match_inch = re.search(r"(\d+(?:\.\d+)?)\s*\"", text)
    if match_inch:
        inches_val = float(match_inch.group(1))
        return f"{inches_val:g}\""

    # Return null if nothing matched
    return ""

def compute_material_from_civil3dInfo(civil3dInfo: str) -> str:
    """
    Determine material code by scanning the Civil3D information string.

    Returns the first key from material_map whose text appears as a
    substring (case-insensitive) in the Civil3D information. If no
    material key is found, returns an empty string.
    """
    if not civil3dInfo:
        return ""

    text_upper = civil3dInfo.upper()
    for material_key in material_map.keys():
        key_upper = str(material_key).upper()
        if key_upper and key_upper in text_upper:
            return material_key

    return ""

def should_skip_row(row: Dict[str, str], fieldnames: List[str]) -> bool:
    """
    Determine if a row should be skipped during processing.

    A row is skipped if the entity_handle column exists in the CSV and is
    empty or contains only whitespace. If the column doesn't exist, rows
    are not skipped.

    Args:
        row: Dictionary containing the CSV row data
        fieldnames: List of column names in the CSV

    Returns:
        True if the row should be skipped, False otherwise
    """
    # Only check entity_handle if the column exists in the CSV
    if INPUT_ENTITY_HANDLE not in fieldnames:
        return False
    
    entity_handle = row.get(INPUT_ENTITY_HANDLE, "").strip()
    return not entity_handle


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

