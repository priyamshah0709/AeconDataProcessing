using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Navisworks.Api;

namespace NavisworksPropertyBaker
{
    public sealed class ModelIndex
    {
        /// <summary>(source file stem, key type, value) -> model item. Primary lookup.</summary>
        public readonly Dictionary<ItemKey, ModelItem> Exact = new Dictionary<ItemKey, ModelItem>();

        public sealed class FallbackEntry
        {
            public ModelItem Item;
            public string SourceFileStem;
        }

        /// <summary>
        /// (key type, value) -> model item, ignoring source file. Used as a fallback when
        /// the CSV's ItemSourceFile doesn't match the model node name (.dwg vs .nwc etc.).
        /// Keys that occur under more than one DIFFERENT source file are moved to
        /// Ambiguous and never matched via fallback; duplicates within one source file
        /// are the same design entity and keep last-wins, matching the exact index.
        /// </summary>
        public readonly Dictionary<KeyOnly, FallbackEntry> ByKeyOnly = new Dictionary<KeyOnly, FallbackEntry>();
        public readonly HashSet<KeyOnly> Ambiguous = new HashSet<KeyOnly>();
    }

    /// <summary>
    /// Builds the identifier index in ONE traversal of the model tree.
    /// Never uses the Search API per CSV row - that is O(rows x items) and is
    /// essentially the DataTools failure mode this plugin replaces.
    /// </summary>
    public sealed class ModelIndexer
    {
        // Identifier lookup is two-tier: stable internal names first, display names
        // as fallback. Run the Diagnostics dump on the real model to confirm these;
        // add confirmed pairs at the FRONT of the internal lists.
        private static readonly string[][] ElementIdInternal =
        {
            // Confirmed from the DNNP federated model diagnostics dump (2026-07-13):
            // Revit element id, stored as a decimal value.
            new[] { "LcRevitId", "LcOaNat64AttributeValue" },
        };
        private static readonly string[][] ElementIdDisplay =
        {
            new[] { "Element ID", "Value" },
        };
        private static readonly string[][] EntityHandleInternal =
        {
            // Confirmed from the DNNP federated model diagnostics dump (2026-07-13):
            // AutoCAD/Civil3D entity handle. Despite the Nat64 internal property name,
            // the value is the hex handle string (e.g. "10DB4"), matching the CSVs.
            new[] { "LcOpDwgEntityAttrib", "LcOaNat64AttributeValue" },
        };
        private static readonly string[][] EntityHandleDisplay =
        {
            new[] { "Entity Handle", "Value" },
        };

        private readonly BakeReport _report;
        private readonly Action<string> _log;

        public ModelIndexer(BakeReport report, Action<string> log)
        {
            _report = report;
            _log = log ?? delegate { };
        }

        public ModelIndex Build(Document doc, Func<bool> isCancelled)
        {
            var index = new ModelIndex();
            var stack = new Stack<KeyValuePair<ModelItem, string>>();

            foreach (Model model in doc.Models)
            {
                string stem = ItemKey.NormalizeStem(model.SourceFileName);
                stack.Push(new KeyValuePair<ModelItem, string>(model.RootItem, stem));
            }

            long visited = 0;
            while (stack.Count > 0)
            {
                var entry = stack.Pop();
                ModelItem item = entry.Key;
                string fileStem = entry.Value;

                // Nested file nodes (federated NWF -> per-discipline NWC/DWG/RVT)
                // update the source-file scope for everything below them.
                if (item.HasModel)
                {
                    string nested = ItemKey.NormalizeStem(item.Model.SourceFileName);
                    if (nested.Length > 0) fileStem = nested;
                }

                visited++;
                if ((visited & 0xFFFF) == 0)
                {
                    _log("  indexed " + visited.ToString("N0") + " items...");
                    if (isCancelled != null && isCancelled()) { _report.Cancelled = true; break; }
                }

                string handle = ReadProperty(item, EntityHandleInternal, EntityHandleDisplay);
                if (!string.IsNullOrEmpty(handle))
                {
                    Add(index, new ItemKey(fileStem, KeyType.EntityHandle,
                        ItemKey.NormalizeValue(KeyType.EntityHandle, handle)), item);
                    _report.ModelItemsWithHandle++;
                }
                else
                {
                    string elementId = ReadProperty(item, ElementIdInternal, ElementIdDisplay);
                    if (!string.IsNullOrEmpty(elementId))
                    {
                        Add(index, new ItemKey(fileStem, KeyType.ElementId,
                            ItemKey.NormalizeValue(KeyType.ElementId, elementId)), item);
                        _report.ModelItemsWithElementId++;
                    }
                }

                foreach (ModelItem child in item.Children)
                    stack.Push(new KeyValuePair<ModelItem, string>(child, fileStem));
            }

            _report.ModelItemsVisited = visited;
            _log("Index built: " + visited.ToString("N0") + " items visited, " +
                 index.Exact.Count.ToString("N0") + " identified (" +
                 _report.ModelItemsWithHandle.ToString("N0") + " handles, " +
                 _report.ModelItemsWithElementId.ToString("N0") + " element ids), " +
                 index.Ambiguous.Count.ToString("N0") + " ambiguous key-only entries.");
            return index;
        }

        private static void Add(ModelIndex index, ItemKey key, ModelItem item)
        {
            // Exact index: last wins (duplicates within one source file are rare; if
            // the same handle appears twice in one file they are the same design entity).
            index.Exact[key] = item;

            KeyOnly ko = key.WithoutFile();
            if (index.Ambiguous.Contains(ko)) return;
            ModelIndex.FallbackEntry prior;
            if (index.ByKeyOnly.TryGetValue(ko, out prior))
            {
                if (string.Equals(prior.SourceFileStem, key.SourceFileStem, StringComparison.Ordinal))
                {
                    prior.Item = item; // same file: same entity, last-wins like the exact index
                }
                else
                {
                    // Same key under a DIFFERENT source file: fallback would be a guess.
                    index.ByKeyOnly.Remove(ko);
                    index.Ambiguous.Add(ko);
                }
            }
            else
            {
                index.ByKeyOnly[ko] = new ModelIndex.FallbackEntry
                {
                    Item = item,
                    SourceFileStem = key.SourceFileStem
                };
            }
        }

        private static string ReadProperty(ModelItem item, string[][] internalPairs, string[][] displayPairs)
        {
            PropertyCategoryCollection cats = item.PropertyCategories;
            if (cats == null) return null;

            foreach (string[] pair in internalPairs)
            {
                DataProperty p = cats.FindPropertyByName(pair[0], pair[1]);
                if (p != null)
                {
                    string v = VariantToString(p.Value);
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            foreach (string[] pair in displayPairs)
            {
                DataProperty p = cats.FindPropertyByDisplayName(pair[0], pair[1]);
                if (p != null)
                {
                    string v = VariantToString(p.Value);
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            return null;
        }

        /// <summary>
        /// Converts a VariantData to an invariant string WITHOUT numeric mangling.
        /// (VariantData.ToString() prefixes the type name, so it is not usable directly.)
        /// </summary>
        public static string VariantToString(VariantData v)
        {
            if (v == null) return null;
            switch (v.DataType)
            {
                case VariantDataType.DisplayString: return v.ToDisplayString();
                case VariantDataType.IdentifierString: return v.ToIdentifierString();
                case VariantDataType.Int32: return v.ToInt32().ToString(CultureInfo.InvariantCulture);
                case VariantDataType.Double: return v.ToDouble().ToString("R", CultureInfo.InvariantCulture);
                case VariantDataType.Boolean: return v.ToBoolean() ? "True" : "False";
                case VariantDataType.NamedConstant:
                    NamedConstant nc = v.ToNamedConstant();
                    return nc != null ? nc.DisplayName : null;
                case VariantDataType.None: return null;
                default:
                    // Length/angle/datetime etc. are never identifiers; strip the
                    // "Type:" prefix ToString() adds as a last resort.
                    string s = v.ToString();
                    int colon = s.IndexOf(':');
                    return colon >= 0 && colon < s.Length - 1 ? s.Substring(colon + 1) : s;
            }
        }
    }
}
