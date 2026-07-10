using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;

namespace NavisworksPropertyBaker
{
    /// <summary>
    /// Writes the enrichment values as STATIC user-defined properties via the COM
    /// bridge. The plugin owns exactly one property tab (options.TabName, default
    /// AECON_DATA): re-runs replace that tab's values in place. Native properties
    /// and any other user tabs (e.g. DataTools tabs) are never touched.
    /// </summary>
    public sealed class PropertyWriter
    {
        private readonly string _tabName;
        private readonly BakeReport _report;
        private readonly Action<string> _log;

        public PropertyWriter(string tabName, BakeReport report, Action<string> log)
        {
            _tabName = tabName;
            _report = report;
            _log = log ?? delegate { };
        }

        public void WriteAll(List<KeyValuePair<ModelItem, PropertyRecord>> matches,
                             Action<double> progress, Func<bool> isCancelled)
        {
            InwOpState10 state = ComApiBridge.State;

            // BeginEdit/EndEdit batching: mandatory workaround for the massive
            // SetUserDefined slowdown introduced in Navisworks 2025 (also speeds up
            // older versions). Called via dynamic so the plugin still runs on interop
            // assemblies that predate the fix.
            bool editScope = TryBeginEdit(state);
            try
            {
                int done = 0;
                foreach (var match in matches)
                {
                    if (isCancelled != null && isCancelled()) { _report.Cancelled = true; break; }

                    ModelItem item = match.Key;
                    PropertyRecord rec = match.Value;

                    InwOaPath path = null;
                    InwGUIPropertyNode2 node = null;
                    InwOaPropertyVec vec = null;
                    try
                    {
                        path = (InwOaPath)ComApiBridge.ToInwOaPath(item);
                        node = (InwGUIPropertyNode2)state.GetGUIPropertyNode(path, true);

                        vec = (InwOaPropertyVec)state.ObjectFactory(
                            nwEObjectType.eObjectType_nwOaPropertyVec, null, null);

                        InwOaPropertyColl props = vec.Properties();
                        try
                        {
                            for (int i = 0; i < rec.Names.Length; i++)
                            {
                                InwOaProperty p = (InwOaProperty)state.ObjectFactory(
                                    nwEObjectType.eObjectType_nwOaProperty, null, null);
                                p.name = rec.Names[i];      // internal name
                                p.UserName = rec.Names[i];  // display name
                                // ALWAYS a string: preserves hex handles, leading zeros,
                                // and dotted account codes like "70.12.04.018".
                                p.value = rec.Values[i] ?? string.Empty;
                                props.Add(p);
                                Marshal.ReleaseComObject(p);
                            }
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(props);
                        }

                        // 0 creates a new tab; a positive index (1-based, counting
                        // ONLY user-defined tabs) replaces that tab in place. This is
                        // what makes re-runs idempotent instead of stacking duplicates,
                        // and what protects other user tabs from being clobbered.
                        int tabIndex = FindUserTabIndex(node, _tabName);
                        node.SetUserDefined(tabIndex, _tabName, _tabName, vec);
                        _report.PropertiesWritten++;
                    }
                    catch (Exception ex)
                    {
                        _report.WriteErrors++;
                        if (_report.WriteErrors <= 20)
                            _log("  WRITE ERROR on " + Describe(rec) + ": " + ex.Message);
                    }
                    finally
                    {
                        // Release COM RCWs eagerly: over 400K+ iterations the finalizer
                        // queue cannot keep up and memory balloons otherwise.
                        if (vec != null) Marshal.ReleaseComObject(vec);
                        if (node != null) Marshal.ReleaseComObject(node);
                        if (path != null) Marshal.ReleaseComObject(path);
                    }

                    done++;
                    if (done % 5000 == 0)
                    {
                        if (progress != null) progress((double)done / matches.Count);
                        if (done % 50000 == 0)
                            _log("  wrote " + done.ToString("N0") + " / " + matches.Count.ToString("N0"));
                    }
                }
            }
            finally
            {
                if (editScope) TryEndEdit(state);
            }
        }

        /// <summary>
        /// Finds the 1-based index of the plugin's tab among USER-DEFINED attributes
        /// only (that is the index space SetUserDefined expects). Returns 0 when the
        /// tab does not exist yet, which tells SetUserDefined to create it.
        /// </summary>
        internal static int FindUserTabIndex(InwGUIPropertyNode2 node, string tabName)
        {
            int userIndex = 0;
            InwGUIAttributesColl attrs = node.GUIAttributes();
            try
            {
                foreach (InwGUIAttribute2 attr in attrs)
                {
                    try
                    {
                        if (!attr.UserDefined) continue;
                        userIndex++;
                        if (string.Equals(attr.ClassUserName, tabName, StringComparison.Ordinal))
                            return userIndex;
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(attr);
                    }
                }
                return 0;
            }
            finally
            {
                Marshal.ReleaseComObject(attrs);
            }
        }

        private bool TryBeginEdit(InwOpState10 state)
        {
            try
            {
                dynamic d = state;
                d.BeginEdit();
                return true;
            }
            catch (Exception)
            {
                _log("  NOTE: state.BeginEdit() not available on this Navisworks version; " +
                     "writing without the batching wrapper.");
                return false;
            }
        }

        private void TryEndEdit(InwOpState10 state)
        {
            try
            {
                dynamic d = state;
                d.EndEdit();
            }
            catch (Exception ex)
            {
                _log("  WARNING: state.EndEdit() failed: " + ex.Message);
            }
        }

        private static string Describe(PropertyRecord rec)
        {
            return rec.SourceCsv + " row " + rec.RowNumber;
        }
    }
}
