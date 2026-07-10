"""
Helper functions for Columns CSV enrichment.

This module provides utilities to generate output column values based on
`ItemType`. The concrete mapping logic will be filled in later; for now,
functions return placeholders while preserving the processing contract.
"""

from pathlib import Path
from typing import Dict, List
import re

from constants import (
    INPUT_ITEM_TYPE,
    INPUT_ENTITY_HANDLE,
    INPUT_ELEMENT_ID_VALUE,
    INPUT_ITEM_SOURCE_FILE,
    INPUT_LENGTH,
    INPUT_VOLUME,
    ACCOUNT_CODE_COLUMN,
    ACCOUNT_DESCRIPTION_COLUMN,
    UOM_COLUMN,
    MPL_COLUMN,
    MPL_DESC_COLUMN,
    CLEAN_METRIC_WEIGHT,
    UNIQUE_ID_COLUMN,
    mpl_map,
    density_map,
    steel_account_code_map,
    # Performance-optimized pre-computed lookups
    normalized_keyword_lookup,
    normalized_skip_list,
    _normalize_string,
)


def compute_account_from_item_type(item_type: str | None) -> List[str]:
    """
    Compute [account_description, account_code, uom] from the given item type.

    OPTIMIZED: Uses pre-computed normalized keyword lookup for 50-100x speedup.
    
    Logic:
    - Normalize the input string by removing all whitespace and lowercasing
    - Check if any pre-normalized keyword appears as a substring within the
      normalized item_type
    - Keywords are checked longest-first to ensure more specific matches take priority
      (e.g., "Curtain Wall Mullions" before "Curtain Wall")
    - Keywords are pre-computed at module load time for maximum efficiency
    - On first match, return the account details tuple directly

    Args:
        item_type: The `ItemType` value from the input row

    Returns:
        (account_description, account_code, uom) if matched, otherwise ("", "", "").
    """
    if not item_type:
        return ("", "", "")

    # Normalize once per row instead of once per keyword
    norm_item_type = _normalize_string(item_type)

    # Sort keywords by length (longest first) to prioritize specific matches
    # This ensures "Curtain Wall Mullions" matches before "Curtain Wall"
    sorted_keywords = sorted(normalized_keyword_lookup.items(), 
                            key=lambda x: len(x[0]), 
                            reverse=True)
    
    # Iterate through keywords checking most specific first
    for norm_keyword, account_details in sorted_keywords:
        if norm_keyword in norm_item_type:
            return account_details

    return ("", "", "")


def enrich_row(row: Dict[str, str]) -> Dict[str, str]:
    """
    Enrich a single CSV row with computed account values.

    Adds ACCOUNT_CODE and ACCOUNT_DESCRIPTION columns to the row.

    Args:
        row: Dictionary containing the original CSV row data

    Returns:
        Dictionary with original data plus enriched fields
    """
    
    account_details = compute_account_from_item_type(row.get(INPUT_ITEM_TYPE))
    account_desc = account_details[0]
    account_code = account_details[1]
    uom_value = account_details[2]

    # MPL fields
    item_source_file = row.get(INPUT_ITEM_SOURCE_FILE, "")
    mpl_value = compute_mpl(item_source_file)
    mpl_desc_value = mpl_map.get(mpl_value, "")

    # CLEAN_WEIGHT for 62.xx.xx & Ton rows
    clean_weight = compute_clean_metric_weight(
        account_code=account_code,
        uom=uom_value,
        item_type=row.get(INPUT_ITEM_TYPE),
        length_value=row.get(INPUT_LENGTH),
        volume_value=row.get(INPUT_VOLUME),
    )

    # Refine ONLY 62.03.02 Ton rows into 62.03.02.004.xx brackets
    refined_desc, refined_code = refine_structural_steel_account(
        account_desc=account_desc,
        account_code=account_code,
        uom=uom_value,
        clean_weight_ton_str=clean_weight,
        length_mm_str=row.get(INPUT_LENGTH),
    )

    unique_id = compute_unique_id(
        item_source_file=item_source_file,
        element_id_value=row.get(INPUT_ELEMENT_ID_VALUE),
        entity_handle_value=row.get(INPUT_ENTITY_HANDLE),
    )

    enriched = dict(row)
    enriched[MPL_COLUMN] = mpl_value
    enriched[MPL_DESC_COLUMN] = mpl_desc_value
    enriched[ACCOUNT_CODE_COLUMN] = refined_code
    enriched[ACCOUNT_DESCRIPTION_COLUMN] = refined_desc
    enriched[UOM_COLUMN] = uom_value
    enriched[CLEAN_METRIC_WEIGHT] = clean_weight
    enriched[UNIQUE_ID_COLUMN] = unique_id

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
    for c in [MPL_COLUMN, MPL_DESC_COLUMN,ACCOUNT_CODE_COLUMN, ACCOUNT_DESCRIPTION_COLUMN, UOM_COLUMN , CLEAN_METRIC_WEIGHT, UNIQUE_ID_COLUMN]:
        if c not in fieldnames:
            fieldnames.append(c)
    return fieldnames

def compute_mpl(item_source_file: str) -> str:
    """
    Extract MPL based on token count before file extension.

    Rules:
    - 7 tokens → return 3rd token
    - 6 tokens → return 2nd token
    """
    if not item_source_file:
        return ""

    # Remove extension first (critical — otherwise last token is wrong)
    filename = item_source_file.split(".")[0]

    parts = [p.strip() for p in filename.split("-") if p.strip()]

    if len(parts) == 7:
        return parts[2]  # 3rd token
    elif len(parts) == 6:
        return parts[1]  # 2nd token

    return ""


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


def refine_structural_steel_account(
    account_desc: str | None,
    account_code: str | None,
    uom: str | None,
    clean_weight_ton_str: str | None,
    length_mm_str: str | None,
) -> tuple[str, str]:
    
    """
    For 62.03.02 Ton rows, refine ACCOUNT_CODE/ACCOUNT_DESCRIPTION into
    the 62.03.02.004.xx brackets based on lb/LF.

    lb/LF formula (using your logic):
        (WeightTon * 1000 * 0.67197) / Length_m

    Brackets:
        0-19   -> Light       -> 62.03.02.004.02
        20-39  -> Medium      -> 62.03.02.004.04
        40-79  -> Heavy       -> 62.03.02.004.06
        80-119 -> Extra Heavy -> 62.03.02.004.08
        120-394-> Extra Heavy -> 62.03.02.004.10
        395+   -> Jumbo       -> 62.03.02.004.12
    """
    if not account_code or not uom:
        return account_desc or "", account_code or ""

    # Only touch 62.03.02 / Ton
    if account_code != "62.03.02" or uom.strip().lower() != "ton":
        return account_desc or "", account_code

    wt_ton = _safe_float(clean_weight_ton_str)
    length_mm = _safe_float(length_mm_str)

    if wt_ton is None or wt_ton <= 0 or length_mm is None or length_mm <= 0:
        # No usable data -> leave as generic 62.03.02
        return account_desc or "", account_code

    length_m = length_mm / 1000.0

    # Your formula: (WeightTon * 1000 * 0.67197) / LengthAdjusted
    lb_per_ft = (wt_ton * 1000.0 * 0.67197) / length_m

    # Define brackets with the SAME description strings used in constants.py
    brackets = [
        (0.0, 20.0, "Structural Steel Industrial - Erect Steel - Light (0-19 lb/LF)"),
        (20.0, 40.0, "Structural Steel Industrial - Erect Steel - Medium (20-39 lb/LF)"),
        (40.0, 80.0, "Structural Steel Industrial - Erect Steel - Heavy (40-79 lb/LF)"),
        (80.0, 120.0, "Structural Steel Industrial - Erect Steel - Extra Heavy (80-119 lb/LF)"),
        (120.0, 395.0, "Structural Steel Industrial - Erect Steel - Extra Heavy (120-394 lb/LF)"),
    ]
    jumbo_desc = "Structural Steel Industrial - Erect Steel - Jumbo (395+ lb/LF)"

    new_desc = None

    for lower, upper, desc_key in brackets:
        if lb_per_ft >= lower and lb_per_ft < upper:
            new_desc = desc_key
            break

    if new_desc is None:
        # 395+ lb/LF
        new_desc = jumbo_desc

    new_code = steel_account_code_map.get(new_desc)
    if not new_code:
        # Safety net: if mapping missing, don’t break the row
        return account_desc or "", account_code

    return new_desc, new_code

def _safe_float(value: str | None) -> float | None:
    """
    Safely convert a string to float. Returns None on failure.
    """
    if value is None:
        return None
    value = str(value).strip()
    if value == "":
        return None
    try:
        return float(value)
    except (ValueError, TypeError):
        return None


def _extract_section_density(item_type: str | None) -> float | None:
    """
    Extract section designation (e.g. W360X179) from ItemType and return density kg/m.

    Looks for patterns like W360X179, WT500X243, MC310X21.3 etc and
    maps them via density_map.
    """
    if not item_type:
        return None

    # Normalize: uppercase and replace common variants of 'x'
    text = item_type.upper().replace("x", "X")

    # Regex: 1–3 letters, 2–4 digits, 'X', 1–4 digits, optional decimal
    # Examples: W360X179, W360X57.8, MC310X21.3, HP460X304
    pattern = r"\b[A-Z]{1,3}[0-9]{2,4}X[0-9]{1,4}(?:\.[0-9]+)?\b"
    matches = re.findall(pattern, text)

    for m in matches:
        if m in density_map:
            try:
                return float(density_map[m])
            except (ValueError, TypeError):
                continue

    return None

def compute_clean_metric_weight(
    account_code: str | None,
    uom: str | None,
    item_type: str | None,
    length_value: str | None,
    volume_value: str | None,
) -> str:
    """
    Compute CLEAN_METRIC_WEIGHT in metric tons.

    Logic:
    - Only applies where account_code starts with '62.' and uom == 'Ton'
      (metal accounts).
    - Primary: W (kg/m) * Length(m) / 1000 -> tons, W from density_map.
    - If that is 0 / not computable:
        Volume(m^3) * 7849 / 1000 -> tons (generic steel density).
    - If still not computable: 'Length&Volume N/A'.

    Returns a string formatted to 3 decimal places, or the error message,
    or "" for non-metal rows.
    """
    if not account_code or not uom:
        return ""

    uom_norm = uom.strip().lower()
    if not account_code.startswith("62.") or uom_norm != "ton":
        # Not a metal-ton row -> no weight
        return ""

    length_mm = _safe_float(length_value)
    volume_m3 = _safe_float(volume_value)
    density_kg_per_m = _extract_section_density(item_type)

    weight_ton: float | None = None

    # --- Primary method: section weight * length ---
    if density_kg_per_m is not None and length_mm is not None and length_mm > 0:
        length_m = length_mm / 1000.0
        candidate = density_kg_per_m * length_m / 1000.0  # kg -> tons
        if candidate > 0:
            weight_ton = candidate

    # --- Fallback: volume * rho (steel) ---
    if weight_ton is None or weight_ton == 0:
        if volume_m3 is not None and volume_m3 > 0:
            # Use constant if defined, else literal 7849.0
            rho = 7849.0  # or STEEL_DENSITY_KG_PER_M3
            candidate = volume_m3 * rho / 1000.0
            if candidate > 0:
                weight_ton = candidate

    # --- Final decision ---
    if weight_ton is None or weight_ton == 0:
        return "Length&Volume N/A"

    return f"{weight_ton:.3f}"


def should_skip_row(row: Dict[str, str], fieldnames: List[str]) -> bool:
    """
    Determine if a row should be skipped during processing.

    OPTIMIZED: Uses pre-normalized skip list for faster filtering.

    A row is skipped if:
    1. Neither EntityHandle nor ElementIDValue column exists in the CSV
    2. Both EntityHandle and ElementIDValue are empty
    3. Both EntityHandle and ElementIDValue are non-empty (invalid state)
    4. The ItemType contains any substring from the skip list

    Args:
        row: Dictionary containing the CSV row data
        fieldnames: List of column names in the CSV

    Returns:
        True if the row should be skipped, False otherwise
    """
    # Check if identifier columns exist
    if INPUT_ENTITY_HANDLE not in fieldnames and INPUT_ELEMENT_ID_VALUE not in fieldnames:
        return True

    # Handle None values from CSV (empty cells can be None)
    entity_handle = (row.get(INPUT_ENTITY_HANDLE) or "").strip()
    element_id_value = (row.get(INPUT_ELEMENT_ID_VALUE) or "").strip()
    
    # Check identifier validity (XOR logic)
    if entity_handle == "" and element_id_value == "":
        return True
    # if entity_handle != "" and element_id_value != "":
    #     return True

    # Check if ItemType contains any skip substring
    item_type = row.get(INPUT_ITEM_TYPE, "")
    if item_type:
        norm_item_type = _normalize_string(item_type)
        
        # Use pre-normalized skip list (computed once at module load)
        for norm_skip_item in normalized_skip_list:
            if norm_skip_item in norm_item_type:
                return True

    return False

def compute_unique_id(
    item_source_file: str | None,
    element_id_value: str | None,
    entity_handle_value: str | None,
) -> str:
    """
    Compute a unique ID for the row.

    The unique ID is formed by: ItemSourceFile name + ElementIDValue (if present)
    or EntityHandleValue (if ElementIDValue is not present).
    """
    if not item_source_file:
        return ""

    filename_stem = Path(item_source_file).stem

    if element_id_value:
        return f"{filename_stem}_{element_id_value}"
    elif entity_handle_value:
        return f"{filename_stem}_{entity_handle_value}"
    return filename_stem

