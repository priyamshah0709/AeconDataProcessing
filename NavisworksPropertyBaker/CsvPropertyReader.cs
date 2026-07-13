using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace NavisworksPropertyBaker
{
    /// <summary>
    /// Streams the enriched CSVs produced by PipesProcessing / ColumnsProcessing
    /// (comma-delimited, UTF-8 with BOM, QUOTE_ALL) and projects each row down to
    /// (ItemKey, PropertyRecord). Never materializes whole files in memory beyond
    /// the record dictionary itself.
    /// </summary>
    public sealed class CsvPropertyReader
    {
        // Header aliases seen across the pipeline outputs (case-insensitive).
        private static readonly string[] EntityHandleHeaders = { "EntityHandleValue", "EntityHandle" };
        private static readonly string[] ElementIdHeaders = { "ElementIDValue", "ElementID" };
        private static readonly string[] SourceFileHeaders = { "ItemSourceFile" };

        // Enrichment columns to bake, in preferred display order. Only the ones
        // actually present in a given CSV are attached. All values stay text.
        private static readonly string[] KnownPropertyColumns =
        {
            "MPL", "SYSTEM_MPL", "MPL_DESCRIPTION",
            "ACCOUNT_CODE", "ACCOUNT_CODE_DESCRIPTION", "UOM",
            "CLEAN_SIZE", "CLEAN_MATERIAL", "CLEAN_WEIGHT", "UNIQUE_ID"
        };

        public sealed class LoadResult
        {
            public Dictionary<ItemKey, PropertyRecord> Records =
                new Dictionary<ItemKey, PropertyRecord>();
            public List<string> DuplicateLines = new List<string>(); // pre-formatted CSV lines
        }

        private readonly BakeReport _report;
        private readonly Action<string> _log;

        public CsvPropertyReader(BakeReport report, Action<string> log)
        {
            _report = report;
            _log = log ?? delegate { };
        }

        public LoadResult Load(IEnumerable<string> csvPaths)
        {
            var result = new LoadResult();
            foreach (string path in csvPaths)
                LoadFile(path, result);
            _report.RecordsLoaded = result.Records.Count;
            return result;
        }

        private void LoadFile(string path, LoadResult result)
        {
            _log("Reading " + path);
            using (var parser = new TextFieldParser(path))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                parser.TrimWhiteSpace = false;

                if (parser.EndOfData)
                {
                    _log("  WARNING: empty file, skipped.");
                    return;
                }

                string[] header = parser.ReadFields();
                var colIndex = BuildHeaderIndex(header);

                int ehCol = FindColumn(colIndex, EntityHandleHeaders);
                int eidCol = FindColumn(colIndex, ElementIdHeaders);
                int srcCol = FindColumn(colIndex, SourceFileHeaders);

                if (ehCol < 0 && eidCol < 0)
                    throw new InvalidDataException(
                        path + ": no key column found. Expected one of " +
                        string.Join("/", EntityHandleHeaders) + " or " + string.Join("/", ElementIdHeaders) + ".");

                // Property columns present in this file, using the CSV's own header
                // spelling as the display name (e.g. SYSTEM_MPL stays SYSTEM_MPL).
                var propNames = new List<string>();
                var propCols = new List<int>();
                foreach (string known in KnownPropertyColumns)
                {
                    int idx;
                    if (colIndex.TryGetValue(known, out idx))
                    {
                        // Normalize like BuildHeaderIndex did: a padded or BOM-carrying
                        // header must not become the baked property's name.
                        propNames.Add(header[idx].Trim().TrimStart('\uFEFF'));
                        propCols.Add(idx);
                    }
                }
                if (propCols.Count == 0)
                    throw new InvalidDataException(
                        path + ": none of the enrichment columns (" + string.Join(", ", KnownPropertyColumns) +
                        ") were found - is this an enriched CSV?");

                string[] sharedNames = propNames.ToArray();
                _log("  key columns: " +
                     (ehCol >= 0 ? header[ehCol] : "-") + " / " + (eidCol >= 0 ? header[eidCol] : "-") +
                     "; properties: " + string.Join(", ", sharedNames));

                string csvFileName = Path.GetFileName(path);
                long rowNum = 1; // header was row 1

                while (!parser.EndOfData)
                {
                    string[] fields;
                    try { fields = parser.ReadFields(); }
                    catch (MalformedLineException ex)
                    {
                        _log("  WARNING: malformed line " + ex.LineNumber + ", skipped.");
                        rowNum++;
                        _report.CsvRowsTotal++;
                        continue;
                    }
                    rowNum++;
                    _report.CsvRowsTotal++;

                    string eh = ehCol >= 0 && ehCol < fields.Length ? fields[ehCol].Trim() : string.Empty;
                    string eid = eidCol >= 0 && eidCol < fields.Length ? fields[eidCol].Trim() : string.Empty;

                    bool hasEh = eh.Length > 0;
                    bool hasEid = eid.Length > 0;

                    if (!hasEh && !hasEid) { _report.CsvRowsSkippedNoKey++; continue; }

                    // ElementID takes precedence. Some enriched exports carry BOTH columns
                    // on Revit rows, where EntityHandleValue is actually a Revit UniqueId
                    // (a GUID like "b012f604-...-000754e8"), NOT an AutoCAD hex handle. The
                    // model has no property matching that GUID, so the real Revit key is
                    // ElementIDValue. DWG rows carry only the hex handle (no ElementID).
                    if (hasEh && hasEid) _report.CsvRowsSkippedBothKeys++; // informational; not skipped

                    KeyType type = hasEid ? KeyType.ElementId : KeyType.EntityHandle;
                    string value = ItemKey.NormalizeValue(type, hasEid ? eid : eh);
                    string srcRaw = srcCol >= 0 && srcCol < fields.Length ? fields[srcCol] : string.Empty;
                    var key = new ItemKey(ItemKey.NormalizeStem(srcRaw), type, value);

                    var values = new string[propCols.Count];
                    for (int i = 0; i < propCols.Count; i++)
                        values[i] = propCols[i] < fields.Length ? fields[propCols[i]] : string.Empty;

                    var record = new PropertyRecord
                    {
                        Names = sharedNames,
                        Values = values,
                        SourceCsv = csvFileName,
                        RowNumber = rowNum,
                        ItemSourceFileRaw = srcRaw
                    };

                    PropertyRecord existing;
                    if (result.Records.TryGetValue(key, out existing))
                    {
                        _report.CsvDuplicateKeys++;
                        result.DuplicateLines.Add(CsvReportWriter.Line(
                            key.Type.ToString(), key.Value, srcRaw,
                            existing.SourceCsv, existing.RowNumber.ToString(),
                            csvFileName, rowNum.ToString()));
                    }
                    result.Records[key] = record; // last-wins
                }
            }
        }

        private static Dictionary<string, int> BuildHeaderIndex(string[] header)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                string name = (header[i] ?? string.Empty).Trim().TrimStart('\uFEFF');
                if (name.Length > 0 && !map.ContainsKey(name))
                    map[name] = i;
            }
            return map;
        }

        private static int FindColumn(Dictionary<string, int> colIndex, string[] aliases)
        {
            foreach (string a in aliases)
            {
                int idx;
                if (colIndex.TryGetValue(a, out idx)) return idx;
            }
            return -1;
        }
    }
}
