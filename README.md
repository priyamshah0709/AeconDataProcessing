# CSV Enrichment Tool

A modular Python tool for enriching CSV files with Material Price List (MPL) codes, account codes, and Unit of Measure (UOM) data for piping specifications.

## 📋 Overview

This tool processes CSV files containing piping data and automatically enriches them with:
- **MPL Code**: Extracted from the ItemSourceFile field
- **MPL Description**: Detailed description based on the MPL code
- **Account Code**: Numeric code for accounting purposes
- **Account Code Description**: Detailed piping specification description
- **UOM**: Unit of Measure (default: LM - Linear Meter)

## 📁 Project Structure

```
Aecon/
├── constants.py                 # Data maps and configuration constants
├── helpers.py                   # Helper functions for data processing
├── enrich_csv_standalone.py     # Main processing script
├── sample.csv                   # Sample input CSV for testing
└── README.md                    # This file
```

## 🔧 Prerequisites

- **Python 3.10+** (uses modern type hints with `|` syntax)
- No external dependencies required (uses only Python standard library)

## 🚀 Quick Start

### 1. Verify Python Version

```bash
python3 --version
```

Ensure you have Python 3.10 or higher.

### 2. Run with Sample Data

```bash
python3 enrich_csv_standalone.py --input sample.csv
```

This will create `sample_enriched.csv` in the same directory.

### 3. Run with Custom Input/Output

```bash
# Specify custom output path
python3 enrich_csv_standalone.py --input sample.csv --output output/enriched_data.csv

# With absolute paths
python3 enrich_csv_standalone.py --input /path/to/input.csv --output /path/to/output.csv
```

## 📝 Usage

### Command-Line Arguments

```bash
python3 enrich_csv_standalone.py --input <input_file> [--output <output_file>]
```

**Arguments:**
- `--input` (required): Path to input CSV file
- `--output` (optional): Path for output CSV file (default: `<input>_enriched.csv`)

### Examples

```bash
# Basic usage with default output
python3 enrich_csv_standalone.py --input data.csv

# Specify output file
python3 enrich_csv_standalone.py --input data.csv --output enriched_data.csv

# Process file in different directory
python3 enrich_csv_standalone.py --input ../data/input.csv --output ../data/output.csv
```

## 📊 Input CSV Format

Your input CSV must contain the following columns:

| Column Name | Description | Example |
|-------------|-------------|---------|
| `ItemSourceFile` | Source file identifier (format: PREFIX-PROJECT-**MPL**-NUMBER) | GEH-BWRX-300-A10-001 |
| `AutoCADCOG_Z` | Z-coordinate (determines above/underground) | 5.2 (above ground), -3.5 (underground) |
| `AutoCADSize` | Pipe size with optional fractions | 2.5", 1 1/2, 3/4 |
| `AutoCADPlantMaterial` | Material code | SS, CS, PVC, Alloy |

**Note:** Additional columns in your CSV will be preserved in the output.

### Sample Input

```csv
ItemSourceFile,AutoCADCOG_Z,AutoCADSize,AutoCADPlantMaterial,Description
GEH-BWRX-300-A10-001,5.2,2.5",SS,Small bore stainless steel pipe
GEH-BWRX-300-B21-002,-3.5,14",CS,Underground carbon steel pipe
```

## 📤 Output CSV Format

The output CSV contains all original columns plus five new enriched columns:

| Column Name | Description | Example |
|-------------|-------------|---------|
| `MPL` | Extracted MPL code | A10 |
| `MPL_DESCRIPTION` | Full MPL description | GENERAL ENGINEERING DOCUMENTS |
| `ACCOUNT_CODE` | Numeric account code | 70.12.04.018 |
| `ACCOUNT_CODE_DESCRIPTION` | Detailed pipe specification | Above Ground Large Bore Pipe (Stainless Steel) (2.5"-6" Diameter) |
| `UOM` | Unit of Measure | LM |

## ⚙️ Configuration

### Customizing Column Names

All column names are configurable in `constants.py`:

```python
# Input column names (columns to read from source CSV)
INPUT_ITEM_SOURCE_FILE = "ItemSourceFile"
INPUT_AUTOCAD_COG_Z = "AutoCADCOG_Z"
INPUT_AUTOCAD_SIZE = "AutoCADSize"
INPUT_AUTOCAD_PLANT_MATERIAL = "AutoCADPlantMaterial"

# Output column names (columns to write to enriched CSV)
MPL_COLUMN = "MPL"
MPL_DESC_COLUMN = "MPL_DESCRIPTION"
ACCOUNT_CODE_COLUMN = "ACCOUNT_CODE"
ACCOUNT_CODE_DESC_COLUMN = "ACCOUNT_CODE_DESCRIPTION"
UOM_COLUMN = "UOM"
```

### Customizing Default Values

```python
# Default values
DEFAULT_UOM = "LM"  # Change to your preferred unit

# Threshold values
GROUND_LEVEL_THRESHOLD = 0.0  # Z-coordinate above this value is considered "above ground"
```

**Important:** The `GROUND_LEVEL_THRESHOLD` determines what qualifies as "above ground" vs "underground":
- Z-coordinates **greater than** this value are classified as **Above Ground**
- Z-coordinates **less than or equal to** this value are classified as **Underground**
- Default is `0.0` (standard ground level)
- Adjust this if your project uses a different baseline elevation

### Adding/Modifying Data Maps

Edit the dictionaries in `constants.py`:
- `mpl_map`: MPL codes to descriptions (99 entries)
- `material_map`: Material codes to full names (4 entries)
- `piping_map`: Pipe descriptions to account codes (178 entries)

## 🏗️ Architecture

### Design Principles

The codebase follows these principles:
- **Single Responsibility**: Each module has one clear purpose
- **Modularity**: Easy to maintain and extend
- **No Hardcoded Values**: All configuration is centralized
- **Type Safety**: Uses type hints throughout
- **Comprehensive Documentation**: Docstrings for all public functions

### Module Descriptions

#### `constants.py`
Contains all static data and configuration:
- Input/output column name constants
- Default values
- MPL, material, and piping mapping dictionaries

#### `helpers.py`
Helper functions for data transformation:
- `compute_mpl()`: Extract MPL code from ItemSourceFile
- `parse_autocad_size()`: Parse various size formats (decimals, fractions, mixed numbers)
- `compute_pipe_size_range()`: Categorize pipe sizes into ranges
- `compute_account_description()`: Generate account descriptions based on pipe specs
- `enrich_row()`: Main enrichment function that processes a single row
- `ensure_fieldnames_with_appends()`: Manage CSV headers
- `compute_output_path()`: Generate output file paths

#### `enrich_csv_standalone.py`
Main processing script:
- `enrich_csv()`: Process entire CSV file
- `main()`: CLI entry point with argument parsing

## 🧪 Testing the Sample

Run the included sample:

```bash
python3 enrich_csv_standalone.py --input sample.csv
```

Expected output file: `sample_enriched.csv`

Verify the enrichment by checking:
1. MPL codes are extracted from ItemSourceFile
2. Pipe classifications (above/underground, small/large bore) are correct
3. Material types are properly mapped
4. Account codes match the descriptions

## 🐛 Troubleshooting

### Common Issues

**Error: "Input CSV has no header row"**
- Ensure your CSV file has a header row with column names
- Check that the file is not empty

**Missing enrichment data (empty MPL or Account Code)**
- Verify input column names match configuration in `constants.py`
- Check that ItemSourceFile follows the format: `PREFIX-PROJECT-MPL-NUMBER`
- Ensure AutoCAD columns contain valid data

**Python version error**
- This tool requires Python 3.10+ for modern type hint syntax
- Upgrade Python: `brew install python@3.11` (macOS) or download from python.org

**File encoding issues**
- The tool handles UTF-8 with BOM (utf-8-sig) for input
- Output is always UTF-8
- If you have encoding issues, convert your CSV to UTF-8

## 📈 Extending the Tool

### Adding New Enrichment Fields

1. Add constants in `constants.py`
2. Create helper function in `helpers.py`
3. Update `enrich_row()` to include new field
4. Update `ensure_fieldnames_with_appends()` to include new column

### Adding New Data Maps

1. Add dictionary to `constants.py`
2. Use directly in `helpers.py` with `.get()` method
3. Update documentation

## 📄 License

Internal tool for Aecon project use.

## 🤝 Contributing

When making changes:
1. Follow existing code style and naming conventions
2. Add docstrings to all functions
3. Keep files under 500 lines
4. Test with sample.csv before committing
5. Update README if adding new features

## 📞 Support

For questions or issues, contact the project maintainer.

---

**Last Updated:** October 28, 2025

