using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Navisworks.Api;

namespace NavisworksPropertyBaker
{
    /// <summary>
    /// Dumps the property inventory of the first N items per source file to a CSV so
    /// the identifier lookup (internal category/property names for Entity Handle and
    /// Element ID) can be verified against the real federated model BEFORE trusting
    /// the matcher. Run this first on any new model; add confirmed internal-name
    /// pairs to the candidate lists at the top of ModelIndexer.
    /// </summary>
    public static class DiagnosticDumper
    {
        private const int ItemsPerSourceFile = 200;

        public static string Dump(Document doc, string reportDir, Action<string> log)
        {
            Directory.CreateDirectory(reportDir);
            string outPath = Path.Combine(reportDir,
                "property_dump_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");

            var perFileCount = new Dictionary<string, int>();
            var lines = new List<string>();
            long visited = 0;

            var stack = new Stack<KeyValuePair<ModelItem, string>>();
            foreach (Model model in doc.Models)
                stack.Push(new KeyValuePair<ModelItem, string>(
                    model.RootItem, ItemKey.NormalizeStem(model.SourceFileName)));

            while (stack.Count > 0)
            {
                var entry = stack.Pop();
                ModelItem item = entry.Key;
                string fileStem = entry.Value;

                if (item.HasModel)
                {
                    string nested = ItemKey.NormalizeStem(item.Model.SourceFileName);
                    if (nested.Length > 0) fileStem = nested;
                }

                visited++;

                int count;
                perFileCount.TryGetValue(fileStem, out count);
                if (count < ItemsPerSourceFile && item.PropertyCategories != null)
                {
                    bool dumpedAny = false;
                    foreach (PropertyCategory cat in item.PropertyCategories)
                    {
                        foreach (DataProperty prop in cat.Properties)
                        {
                            lines.Add(CsvReportWriter.Line(
                                fileStem,
                                item.DisplayName ?? string.Empty,
                                item.ClassDisplayName ?? string.Empty,
                                item.HasGeometry ? "1" : "0",
                                cat.DisplayName ?? string.Empty,
                                cat.Name ?? string.Empty,
                                prop.DisplayName ?? string.Empty,
                                prop.Name ?? string.Empty,
                                ModelIndexer.VariantToString(prop.Value) ?? string.Empty));
                            dumpedAny = true;
                        }
                    }
                    if (dumpedAny) perFileCount[fileStem] = count + 1;
                }

                foreach (ModelItem child in item.Children)
                    stack.Push(new KeyValuePair<ModelItem, string>(child, fileStem));
            }

            CsvReportWriter.Write(outPath,
                CsvReportWriter.Line("SourceFileStem", "ItemDisplayName", "ItemClass", "HasGeometry",
                                     "CategoryDisplayName", "CategoryInternalName",
                                     "PropertyDisplayName", "PropertyInternalName", "Value"),
                lines);

            if (log != null)
                log("Diagnostics: visited " + visited.ToString("N0") + " items, dumped " +
                    lines.Count.ToString("N0") + " property rows for " + perFileCount.Count +
                    " source files to " + outPath);
            return outPath;
        }
    }
}
