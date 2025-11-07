"""
Helper functions for Columns CSV enrichment.

This module provides utilities to generate output column values based on
`ItemType`. The concrete mapping logic will be filled in later; for now,
functions return placeholders while preserving the processing contract.
"""

from pathlib import Path
from typing import Dict, List

from constants import (
    INPUT_ITEM_TYPE,
    INPUT_ENTITY_HANDLE,
    INPUT_ITEM_SOURCE_FILE,
    ACCOUNT_CODE_COLUMN,
    ACCOUNT_DESCRIPTION_COLUMN,
    UOM_COLUMN,
    MPL_COLUMN,
    MPL_DESC_COLUMN,
    # account_desc_to_code_map,
    # account_code_to_uom_map,
    description_to_items,
    item_type_to_skip,
    mpl_map,
)


def compute_account_from_item_type(item_type: str | None) -> List[str]:
    """
    Compute [account_description, account_code, uom] from the given item type.

    Logic:
    - Normalize the input and candidate strings by removing all whitespace
      and lowercasing.
    - Iterate each entry in `description_to_items` and check whether any of
      its item identifiers appears as a substring within the provided
      `item_type`.
    - Keys are always a tuple in the form (account_description, account_code, uom).
    - On first match, return the key list directly.

    Args:
        item_type: The `ItemType` value from the input row

    Returns:
        (account_description, account_code, uom) if matched, otherwise ("", "", "").
    """
    if not item_type:
        return ("", "", "")

    def _norm(s: str) -> str:
        # remove all whitespace and lowercase
        return "".join(s.lower().split())

    norm_item_type = _norm(item_type)

    for account_details, possible_keywords_list in description_to_items.items():
        for ident in possible_keywords_list:
            if _norm(ident) and _norm(ident) in norm_item_type:
                # Keys are guaranteed to be list[str]
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

    # MPL fields (same logic as PipesProcessing)
    item_source_file = row.get(INPUT_ITEM_SOURCE_FILE, "")
    mpl_value = compute_mpl(item_source_file)
    mpl_desc_value = mpl_map.get(mpl_value, "")

    enriched = dict(row)
    enriched[MPL_COLUMN] = mpl_value
    enriched[MPL_DESC_COLUMN] = mpl_desc_value
    enriched[ACCOUNT_CODE_COLUMN] = account_code
    enriched[ACCOUNT_DESCRIPTION_COLUMN] = account_desc
    enriched[UOM_COLUMN] = uom_value
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
    for c in [ACCOUNT_CODE_COLUMN, ACCOUNT_DESCRIPTION_COLUMN, UOM_COLUMN, MPL_COLUMN, MPL_DESC_COLUMN]:
        if c not in fieldnames:
            fieldnames.append(c)
    return fieldnames


def compute_mpl(item_source_file: str) -> str:
    """
    Extract MPL code from ItemSourceFile.

    The MPL code is the third hyphen-separated token.
    """
    if not item_source_file:
        return ""
    parts = item_source_file.split("-")
    if len(parts) < 3:
        return ""
    return parts[2].strip()


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


def should_skip_row(row: Dict[str, str], fieldnames: List[str]) -> bool:
    """
    Determine if a row should be skipped during processing.

    A row is skipped if:
    1. The `EntityHandle` column doesn't exist in the CSV
    2. The `EntityHandle` column is empty or contains only whitespace
    3. The `ItemType` contains any substring from the skip list

    Args:
        row: Dictionary containing the CSV row data
        fieldnames: List of column names in the CSV

    Returns:
        True if the row should be skipped, False otherwise
    """
    # Check if EntityHandle column exists
    if INPUT_ENTITY_HANDLE not in fieldnames:
        return True

    # Check if EntityHandle is empty
    entity_handle = row.get(INPUT_ENTITY_HANDLE, "").strip()
    if entity_handle == "":
        return True

    # Check if ItemType contains any skip substring
    item_type = row.get(INPUT_ITEM_TYPE, "")
    if item_type:
        # Normalize by removing all whitespace and lowercasing
        norm_item_type = "".join(item_type.lower().split())
        
        for skip_item in item_type_to_skip:
            norm_skip_item = "".join(skip_item.lower().split())
            if norm_skip_item and norm_skip_item in norm_item_type:
                return True

    return False

