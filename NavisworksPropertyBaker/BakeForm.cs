using System;
using System.IO;
using System.Windows.Forms;

namespace NavisworksPropertyBaker
{
    /// <summary>
    /// Minimal options dialog for interactive runs. All logic lives in BakeRunner;
    /// this form only collects a BakeOptions.
    /// </summary>
    public sealed class BakeForm : Form
    {
        private readonly ListBox _csvList = new ListBox();
        private readonly TextBox _tabName = new TextBox();
        private readonly TextBox _reportDir = new TextBox();
        private readonly TextBox _outputNwd = new TextBox();
        private readonly ComboBox _mode = new ComboBox();

        public BakeOptions Result { get; private set; }

        /// <summary>Shows the dialog; returns null if the user cancels.</summary>
        public static BakeOptions Prompt()
        {
            using (var form = new BakeForm())
            {
                return form.ShowDialog() == DialogResult.OK ? form.Result : null;
            }
        }

        private BakeForm()
        {
            Text = "AECON Property Baker";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new System.Drawing.Size(640, 420);
            Font = new System.Drawing.Font("Segoe UI", 9f);

            int margin = 12;
            int labelW = 110;
            int fieldX = margin + labelW + 6;
            int fieldW = ClientSize.Width - fieldX - margin - 90;
            int y = margin;

            AddLabel("Enriched CSVs:", margin, y);
            _csvList.SetBounds(fieldX, y, fieldW, 120);
            _csvList.SelectionMode = SelectionMode.MultiExtended;
            _csvList.HorizontalScrollbar = true;
            Controls.Add(_csvList);

            var addBtn = AddButton("Add...", fieldX + fieldW + 6, y, 80);
            addBtn.Click += OnAddCsv;
            var removeBtn = AddButton("Remove", fieldX + fieldW + 6, y + 32, 80);
            removeBtn.Click += (s, e) =>
            {
                for (int i = _csvList.SelectedIndices.Count - 1; i >= 0; i--)
                    _csvList.Items.RemoveAt(_csvList.SelectedIndices[i]);
            };
            y += 132;

            AddLabel("Property tab:", margin, y);
            _tabName.SetBounds(fieldX, y, 200, 24);
            _tabName.Text = "AECON_DATA";
            Controls.Add(_tabName);
            y += 34;

            AddLabel("Mode:", margin, y);
            _mode.SetBounds(fieldX, y, 320, 24);
            _mode.DropDownStyle = ComboBoxStyle.DropDownList;
            _mode.Items.Add("Bake - attach properties to the open model");
            _mode.Items.Add("Dry run - match only, report, write nothing");
            _mode.Items.Add("Diagnostics - dump property names to CSV");
            _mode.SelectedIndex = 1; // default to the safe option
            Controls.Add(_mode);
            y += 34;

            AddLabel("Report folder:", margin, y);
            _reportDir.SetBounds(fieldX, y, fieldW, 24);
            Controls.Add(_reportDir);
            var reportBtn = AddButton("Browse...", fieldX + fieldW + 6, y, 80);
            reportBtn.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    if (dlg.ShowDialog() == DialogResult.OK) _reportDir.Text = dlg.SelectedPath;
                }
            };
            y += 34;

            AddLabel("Save NWD as:", margin, y);
            _outputNwd.SetBounds(fieldX, y, fieldW, 24);
            Controls.Add(_outputNwd);
            var nwdBtn = AddButton("Browse...", fieldX + fieldW + 6, y, 80);
            nwdBtn.Click += (s, e) =>
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "Navisworks NWD (*.nwd)|*.nwd";
                    dlg.Title = "Save baked model as";
                    if (dlg.ShowDialog() == DialogResult.OK) _outputNwd.Text = dlg.FileName;
                }
            };
            y += 30;

            var hint = new Label
            {
                Text = "Leave 'Save NWD as' empty to save manually after the bake. " +
                       "Baking writes static properties into the OPEN document; run a Dry run " +
                       "first on a new model and check the match rate in the report.",
                AutoSize = false
            };
            hint.SetBounds(fieldX, y, fieldW, 48);
            Controls.Add(hint);
            y += 56;

            var okBtn = AddButton("Run", ClientSize.Width - margin - 176, y, 80);
            okBtn.Click += OnRun;
            var cancelBtn = AddButton("Cancel", ClientSize.Width - margin - 86, y, 80);
            cancelBtn.DialogResult = DialogResult.Cancel;
            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }

        private void OnAddCsv(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                dlg.Multiselect = true;
                dlg.Title = "Select enriched CSV file(s)";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                foreach (string f in dlg.FileNames)
                    if (!_csvList.Items.Contains(f)) _csvList.Items.Add(f);
                if (_reportDir.Text.Length == 0 && dlg.FileNames.Length > 0)
                    _reportDir.Text = Path.GetDirectoryName(dlg.FileNames[0]);
            }
        }

        private void OnRun(object sender, EventArgs e)
        {
            var options = new BakeOptions
            {
                TabName = _tabName.Text.Trim().Length > 0 ? _tabName.Text.Trim() : "AECON_DATA",
                ReportDir = _reportDir.Text.Trim(),
                OutputNwdPath = _outputNwd.Text.Trim(),
                Mode = _mode.SelectedIndex == 0 ? RunMode.Bake
                     : _mode.SelectedIndex == 1 ? RunMode.DryRun
                     : RunMode.Diagnostics
            };
            foreach (object item in _csvList.Items)
                options.CsvPaths.Add((string)item);

            if (options.Mode != RunMode.Diagnostics && options.CsvPaths.Count == 0)
            {
                MessageBox.Show(this, "Add at least one enriched CSV file.",
                    "AECON Property Baker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (options.ReportDir.Length == 0 && options.CsvPaths.Count > 0)
                options.ReportDir = Path.GetDirectoryName(options.CsvPaths[0]);

            Result = options;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void AddLabel(string text, int x, int y)
        {
            var l = new Label { Text = text, AutoSize = false };
            l.SetBounds(x, y + 3, 110, 20);
            Controls.Add(l);
        }

        private Button AddButton(string text, int x, int y, int width)
        {
            var b = new Button { Text = text };
            b.SetBounds(x, y, width, 26);
            Controls.Add(b);
            return b;
        }
    }
}
