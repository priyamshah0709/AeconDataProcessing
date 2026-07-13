using System;
using System.Collections.Generic;
using System.IO;

namespace NavisworksPropertyBaker
{
    public enum KeyType : byte
    {
        EntityHandle = 0,
        ElementId = 1
    }

    /// <summary>
    /// Identity of one taggable element: (source file stem, key type, key value).
    /// Entity handles are only unique per source DWG and Revit element IDs per RVT,
    /// so the source file stem is part of the key.
    /// </summary>
    public struct ItemKey : IEquatable<ItemKey>
    {
        public readonly string SourceFileStem; // lower-case file name without extension
        public readonly KeyType Type;
        public readonly string Value;          // handle upper-cased hex text, element id trimmed text

        public ItemKey(string sourceFileStem, KeyType type, string value)
        {
            SourceFileStem = sourceFileStem ?? string.Empty;
            Type = type;
            Value = value ?? string.Empty;
        }

        public bool Equals(ItemKey other)
        {
            return Type == other.Type
                && string.Equals(Value, other.Value, StringComparison.Ordinal)
                && string.Equals(SourceFileStem, other.SourceFileStem, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return obj is ItemKey && Equals((ItemKey)obj); }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + Type.GetHashCode();
                h = h * 31 + (Value != null ? Value.GetHashCode() : 0);
                h = h * 31 + (SourceFileStem != null ? SourceFileStem.GetHashCode() : 0);
                return h;
            }
        }

        public override string ToString()
        {
            return SourceFileStem + "|" + Type + "|" + Value;
        }

        /// <summary>File-stem-agnostic version of the key, used for the fallback index.</summary>
        public KeyOnly WithoutFile() { return new KeyOnly(Type, Value); }

        public static string NormalizeStem(string fileNameOrPath)
        {
            if (string.IsNullOrEmpty(fileNameOrPath)) return string.Empty;
            string stem;
            try { stem = Path.GetFileNameWithoutExtension(fileNameOrPath); }
            catch (ArgumentException) { stem = fileNameOrPath; } // invalid path chars: use raw
            stem = (stem ?? string.Empty).Trim().ToLowerInvariant();
            // Navisworks appends "_detached" to some federated model node names; the
            // enriched CSV source file names do not carry it. Strip it so both sides align.
            if (stem.EndsWith("_detached")) stem = stem.Substring(0, stem.Length - "_detached".Length);
            return string.Intern(stem);
        }

        public static string NormalizeValue(KeyType type, string raw)
        {
            if (raw == null) return string.Empty;
            string v = raw.Trim();
            // Handles are hex-like text ("1841A0") - compare case-insensitively via upper-casing.
            // Element IDs stay as-is; both must never round-trip through numeric types.
            return type == KeyType.EntityHandle ? v.ToUpperInvariant() : v;
        }
    }

    public struct KeyOnly : IEquatable<KeyOnly>
    {
        public readonly KeyType Type;
        public readonly string Value;

        public KeyOnly(KeyType type, string value)
        {
            Type = type;
            Value = value ?? string.Empty;
        }

        public bool Equals(KeyOnly other)
        {
            return Type == other.Type && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return obj is KeyOnly && Equals((KeyOnly)obj); }

        public override int GetHashCode()
        {
            unchecked { return Type.GetHashCode() * 31 + (Value != null ? Value.GetHashCode() : 0); }
        }
    }

    /// <summary>
    /// The enrichment values for one CSV row. Names is shared per CSV file
    /// (one array for all rows of that file) to keep ~900K records cheap.
    /// </summary>
    public sealed class PropertyRecord
    {
        public string[] Names;
        public string[] Values;
        public string SourceCsv;
        public long RowNumber;
        public string ItemSourceFileRaw; // as it appeared in the CSV, for reports
    }

    public enum RunMode
    {
        Diagnostics, // dump property category/name inventory, no matching, no writes
        DryRun,      // read + index + match + reports, no writes
        Bake         // full run: write properties (and optionally save NWD)
    }

    public sealed class BakeOptions
    {
        public List<string> CsvPaths = new List<string>();
        public string TabName = "AECON_DATA";
        public string ReportDir;
        public RunMode Mode = RunMode.Bake;
        public string OutputNwdPath; // optional; empty = user saves manually

        /// <summary>
        /// Headless parameter parsing for
        ///   Roamer.exe -NoGui -ExecuteAddInPlugin PropertyBaker.AECON
        ///       "csv=D:\out\pipes_enriched.csv" "csv=D:\out\cols_enriched.csv"
        ///       "out=D:\out\Federated_baked.nwd" "tab=AECON_DATA"
        ///       "report=D:\out\bake_reports" "mode=bake|dryrun|diag"
        /// </summary>
        public static BakeOptions Parse(string[] parameters)
        {
            var o = new BakeOptions();
            foreach (string raw in parameters)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string k = raw.Substring(0, eq).Trim().ToLowerInvariant();
                string v = raw.Substring(eq + 1).Trim().Trim('"');
                switch (k)
                {
                    case "csv": o.CsvPaths.Add(v); break;
                    case "out": o.OutputNwdPath = v; break;
                    case "tab": if (v.Length > 0) o.TabName = v; break;
                    case "report": o.ReportDir = v; break;
                    case "mode":
                        if (v.Equals("dryrun", StringComparison.OrdinalIgnoreCase)) o.Mode = RunMode.DryRun;
                        else if (v.StartsWith("diag", StringComparison.OrdinalIgnoreCase)) o.Mode = RunMode.Diagnostics;
                        else o.Mode = RunMode.Bake;
                        break;
                }
            }
            if (string.IsNullOrEmpty(o.ReportDir) && o.CsvPaths.Count > 0)
                o.ReportDir = Path.GetDirectoryName(Path.GetFullPath(o.CsvPaths[0]));
            if (o.Mode != RunMode.Diagnostics && o.CsvPaths.Count == 0)
                return null;
            return o;
        }
    }

    public sealed class BakeReport
    {
        public long CsvRowsTotal;
        public long CsvRowsSkippedNoKey;      // neither key present (mirrors should_skip_row)
        public long CsvRowsSkippedBothKeys;   // both keys present (mirrors should_skip_row)
        public long CsvDuplicateKeys;
        public long RecordsLoaded;

        public long ModelItemsVisited;
        public long ModelItemsWithHandle;
        public long ModelItemsWithElementId;

        public long MatchedExact;
        public long MatchedFallback;
        public long UnmatchedNotInModel;
        public long UnmatchedAmbiguous;

        public long PropertiesWritten;   // items written
        public long WriteErrors;

        public TimeSpan CsvReadTime;
        public TimeSpan IndexTime;
        public TimeSpan MatchTime;
        public TimeSpan WriteTime;
        public TimeSpan SaveTime;

        public bool Cancelled;

        public long Matched { get { return MatchedExact + MatchedFallback; } }
        public long Unmatched { get { return UnmatchedNotInModel + UnmatchedAmbiguous; } }

        public string Summary()
        {
            double matchRate = RecordsLoaded > 0 ? 100.0 * Matched / RecordsLoaded : 0.0;
            return
                "CSV rows read:        " + CsvRowsTotal + "\r\n" +
                "  skipped (no key):   " + CsvRowsSkippedNoKey + "\r\n" +
                "  both keys->ElemID:  " + CsvRowsSkippedBothKeys + "\r\n" +
                "  duplicate keys:     " + CsvDuplicateKeys + "\r\n" +
                "Records loaded:       " + RecordsLoaded + "\r\n" +
                "Model items visited:  " + ModelItemsVisited +
                "  (handles: " + ModelItemsWithHandle + ", element ids: " + ModelItemsWithElementId + ")\r\n" +
                "Matched:              " + Matched + " (" + matchRate.ToString("0.00") + "%)" +
                "  [exact: " + MatchedExact + ", fallback: " + MatchedFallback + "]\r\n" +
                "Unmatched:            " + Unmatched +
                "  [not in model: " + UnmatchedNotInModel + ", ambiguous: " + UnmatchedAmbiguous + "]\r\n" +
                "Items written:        " + PropertiesWritten + (WriteErrors > 0 ? "  (errors: " + WriteErrors + ")" : "") + "\r\n" +
                "Timings:  csv " + Fmt(CsvReadTime) + " | index " + Fmt(IndexTime) + " | match " + Fmt(MatchTime) +
                " | write " + Fmt(WriteTime) + " | save " + Fmt(SaveTime) +
                (Cancelled ? "\r\nRUN WAS CANCELLED BEFORE COMPLETION" : "");
        }

        private static string Fmt(TimeSpan t)
        {
            return t.TotalSeconds < 1 ? t.TotalMilliseconds.ToString("0") + "ms" : t.TotalSeconds.ToString("0.0") + "s";
        }
    }
}
