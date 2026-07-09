using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Autodesk.Navisworks.Api;
using NavisApplication = Autodesk.Navisworks.Api.Application;

namespace NavisworksPropertyBaker
{
    /// <summary>
    /// Orchestrates a run: read CSVs -> index model -> match -> (write) -> (save) -> report.
    /// All phases are timed and logged to bake_&lt;timestamp&gt;.log in the report folder;
    /// unmatched and duplicate rows go to their own CSVs for review.
    /// </summary>
    public sealed class BakeRunner
    {
        private readonly Document _doc;
        private readonly BakeOptions _options;
        private readonly BakeReport _report = new BakeReport();
        private StreamWriter _logWriter;

        public BakeRunner(Document doc, BakeOptions options)
        {
            _doc = doc;
            _options = options;
        }

        public BakeReport Report { get { return _report; } }

        public int Run()
        {
            if (_doc == null || _doc.Models.Count == 0)
            {
                Log("ERROR: no document/model is open in Navisworks.");
                return 2;
            }

            string reportDir = _options.ReportDir;
            if (string.IsNullOrEmpty(reportDir))
                reportDir = Path.Combine(Path.GetTempPath(), "NavisworksPropertyBaker");
            Directory.CreateDirectory(reportDir);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logPath = Path.Combine(reportDir, "bake_" + stamp + ".log");

            using (_logWriter = new StreamWriter(logPath, false, new UTF8Encoding(true)))
            {
                _logWriter.AutoFlush = true;
                try
                {
                    Log("NavisworksPropertyBaker " + stamp + "  mode=" + _options.Mode +
                        "  tab=" + _options.TabName);
                    Log("Document: " + (_doc.FileName ?? "(unsaved)"));

                    if (_options.Mode == RunMode.Diagnostics)
                    {
                        DiagnosticDumper.Dump(_doc, reportDir, Log);
                        return 0;
                    }

                    RunPipeline(reportDir, stamp);

                    Log("");
                    Log(_report.Summary());
                    return _report.Cancelled ? 3 : (_report.WriteErrors > 0 ? 1 : 0);
                }
                catch (Exception ex)
                {
                    Log("FATAL: " + ex);
                    return 2;
                }
            }
        }

        private void RunPipeline(string reportDir, string stamp)
        {
            var sw = Stopwatch.StartNew();

            // ---- Phase 1: CSV read -------------------------------------------------
            var reader = new CsvPropertyReader(_report, Log);
            CsvPropertyReader.LoadResult csv = reader.Load(_options.CsvPaths);
            _report.CsvReadTime = sw.Elapsed;
            Log("CSV read done: " + _report.RecordsLoaded.ToString("N0") + " records in " +
                _report.CsvReadTime.TotalSeconds.ToString("0.0") + "s");

            if (csv.DuplicateLines.Count > 0)
            {
                string dupPath = Path.Combine(reportDir, "duplicates_" + stamp + ".csv");
                CsvReportWriter.Write(dupPath,
                    CsvReportWriter.Line("KeyType", "KeyValue", "ItemSourceFile",
                                         "FirstCsv", "FirstRow", "OverwrittenByCsv", "OverwrittenByRow"),
                    csv.DuplicateLines);
                Log("Duplicate keys written to " + dupPath);
            }

            // ---- Phase 2: model index (with progress bar) --------------------------
            Progress progress = NavisApplication.BeginProgress("AECON Property Baker");
            ModelIndex index;
            List<KeyValuePair<ModelItem, PropertyRecord>> matches;
            var unmatchedLines = new List<string>();
            try
            {
                sw.Restart();
                var indexer = new ModelIndexer(_report, Log);
                // BeginProgress can return null (e.g. -NoGui automation or a progress
                // operation already underway) - guard every use.
                index = indexer.Build(_doc, () => progress != null && progress.IsCanceled);
                _report.IndexTime = sw.Elapsed;
                Log("Index done in " + _report.IndexTime.TotalSeconds.ToString("0.0") + "s");
                if (_report.Cancelled) return;

                // ---- Phase 3: match ------------------------------------------------
                sw.Restart();
                matches = Match(csv.Records, index, unmatchedLines);
                _report.MatchTime = sw.Elapsed;
                Log("Match done in " + _report.MatchTime.TotalSeconds.ToString("0.0") + "s: " +
                    _report.Matched.ToString("N0") + " matched, " +
                    _report.Unmatched.ToString("N0") + " unmatched");

                if (unmatchedLines.Count > 0)
                {
                    string unPath = Path.Combine(reportDir, "unmatched_" + stamp + ".csv");
                    CsvReportWriter.Write(unPath,
                        CsvReportWriter.Line("Reason", "KeyType", "KeyValue", "ItemSourceFile",
                                             "SourceCsv", "Row"),
                        unmatchedLines);
                    Log("Unmatched rows written to " + unPath);
                }

                if (_options.Mode == RunMode.DryRun)
                {
                    Log("Dry run: no properties written.");
                    return;
                }

                // ---- Phase 4: write ------------------------------------------------
                Log("Writing " + matches.Count.ToString("N0") + " property tabs...");
                sw.Restart();
                var writer = new PropertyWriter(_options.TabName, _report, Log);
                writer.WriteAll(matches,
                    fraction => { if (progress != null) progress.Update(fraction); },
                    () => progress != null && progress.IsCanceled);
                _report.WriteTime = sw.Elapsed;
                Log("Write done in " + _report.WriteTime.TotalSeconds.ToString("0.0") + "s: " +
                    _report.PropertiesWritten.ToString("N0") + " items written, " +
                    _report.WriteErrors.ToString("N0") + " errors");
                if (_report.Cancelled) return;
            }
            finally
            {
                NavisApplication.EndProgress();
            }

            // ---- Phase 5: save (optional) -------------------------------------------
            if (!string.IsNullOrEmpty(_options.OutputNwdPath))
            {
                sw.Restart();
                Log("Saving " + _options.OutputNwdPath + " ...");
                _doc.SaveFile(_options.OutputNwdPath);
                _report.SaveTime = sw.Elapsed;
                Log("Save done in " + _report.SaveTime.TotalSeconds.ToString("0.0") + "s");
            }
            else
            {
                Log("No output path set - remember to save the document to persist the properties.");
            }
        }

        private List<KeyValuePair<ModelItem, PropertyRecord>> Match(
            Dictionary<ItemKey, PropertyRecord> records,
            ModelIndex index,
            List<string> unmatchedLines)
        {
            var matches = new List<KeyValuePair<ModelItem, PropertyRecord>>(records.Count);

            foreach (var kv in records)
            {
                ItemKey key = kv.Key;
                PropertyRecord rec = kv.Value;

                ModelItem item;
                if (index.Exact.TryGetValue(key, out item))
                {
                    _report.MatchedExact++;
                    matches.Add(new KeyValuePair<ModelItem, PropertyRecord>(item, rec));
                    continue;
                }

                // Fallback: key-only lookup, used only when the key exists under exactly
                // one source file in the model. Covers .dwg (CSV) vs .nwc (model tree)
                // naming drift without ever guessing between duplicates.
                KeyOnly ko = key.WithoutFile();
                ModelIndex.FallbackEntry fallback;
                if (index.Ambiguous.Contains(ko))
                {
                    _report.UnmatchedAmbiguous++;
                    unmatchedLines.Add(CsvReportWriter.Line(
                        "AMBIGUOUS", key.Type.ToString(), key.Value, rec.ItemSourceFileRaw,
                        rec.SourceCsv, rec.RowNumber.ToString()));
                }
                else if (index.ByKeyOnly.TryGetValue(ko, out fallback))
                {
                    _report.MatchedFallback++;
                    matches.Add(new KeyValuePair<ModelItem, PropertyRecord>(fallback.Item, rec));
                }
                else
                {
                    _report.UnmatchedNotInModel++;
                    unmatchedLines.Add(CsvReportWriter.Line(
                        "NOT_IN_MODEL", key.Type.ToString(), key.Value, rec.ItemSourceFileRaw,
                        rec.SourceCsv, rec.RowNumber.ToString()));
                }
            }
            return matches;
        }

        private void Log(string message)
        {
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message;
            if (_logWriter != null) _logWriter.WriteLine(line);
            Debug.WriteLine(line);
        }
    }
}
