using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using NavisApplication = Autodesk.Navisworks.Api.Application;

namespace NavisworksPropertyBaker
{
    /// <summary>
    /// Entry point. Appears under Tool add-ins in Navisworks Manage.
    ///
    /// Interactive: click the button, pick the enriched CSV(s), run.
    /// Headless (later):
    ///   "C:\Program Files\Autodesk\Navisworks Manage 2026\Roamer.exe"
    ///     -OpenFile "D:\models\Federated.nwd"
    ///     -ExecuteAddInPlugin PropertyBaker.AECON
    ///         "csv=D:\out\pipes_enriched.csv" "csv=D:\out\columns_enriched.csv"
    ///         "out=D:\out\Federated_baked.nwd" "mode=bake"
    ///     -NoGui -Exit
    /// </summary>
    [Plugin("PropertyBaker", "AECON",
        DisplayName = "AECON Property Baker",
        ToolTip = "Bulk-attach enriched CSV properties (MPL, Account Code, UOM...) and bake to NWD")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class PropertyBakerPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            bool interactive = parameters == null || parameters.Length == 0;

            BakeOptions options = interactive
                ? BakeForm.Prompt()
                : BakeOptions.Parse(parameters);
            if (options == null)
                return interactive ? 0 : 2; // cancelled / bad parameters

            var runner = new BakeRunner(NavisApplication.ActiveDocument, options);
            int exitCode = runner.Run();

            if (interactive)
            {
                MessageBox.Show(
                    runner.Report.Summary() +
                    "\r\n\r\nDetailed logs are in: " + (options.ReportDir ?? "(temp folder)"),
                    "AECON Property Baker - " + options.Mode,
                    MessageBoxButtons.OK,
                    exitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            return exitCode;
        }
    }
}
