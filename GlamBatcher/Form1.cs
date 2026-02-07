#nullable disable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GlamBatcher
{
    public partial class Form1 : Form
    {
        // --- CONFIGURATION ---
        private const string CurrentVersion = "1.0.0";
        // Hier sind jetzt DEINE echten Links eingetragen:
        private const string VersionUrl = "https://raw.githubusercontent.com/STRNDV/GlamBatcher/main/version.txt";
        private const string DownloadUrl = "https://github.com/STRNDV/GlamBatcher/releases";

        private bool _isLoading = false;

        private string GetGlamourerPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "XIVLauncher", "pluginConfigs", "Glamourer", "designs");
        }

        public Form1()
        {
            InitializeComponent();

            this.Text = $"GlamBatcher v{CurrentVersion}";

            numFace.ValueChanged += (s, e) => { if (!_isLoading) chkFace.Checked = true; };
            numHair.ValueChanged += (s, e) => { if (!_isLoading) chkHair.Checked = true; };
            numTail.ValueChanged += (s, e) => { if (!_isLoading) chkTail.Checked = true; };
            numPaint.ValueChanged += (s, e) => { if (!_isLoading) chkPaint.Checked = true; };

            CheckForUpdate();
        }

        // --- UPDATE CHECKER ---
        private async void CheckForUpdate()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(3);

                    // Fragt GitHub: "Welche Version ist aktuell?"
                    string remoteVersionString = await client.GetStringAsync(VersionUrl);
                    remoteVersionString = remoteVersionString.Trim();

                    Version local = Version.Parse(CurrentVersion);
                    Version remote = Version.Parse(remoteVersionString);

                    if (remote > local)
                    {
                        string msg = $"New version available!\n\nCurrent: {local}\nNew: {remote}\n\nDo you want to download it now?";
                        if (MessageBox.Show(msg, "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo { FileName = DownloadUrl, UseShellExecute = true });
                        }
                    }
                }
            }
            catch { }
        }

        // --- 1. LOAD ---
        private void btnLoad_Click(object sender, EventArgs e)
        {
            string path = GetGlamourerPath();

            if (!Directory.Exists(path))
            {
                MessageBox.Show($"Glamourer folder not found at:\n{path}", "Error");
                return;
            }

            LoadDesignsIntoTree(path);
        }

        private void LoadDesignsIntoTree(string path)
        {
            treeViewDesigns.Nodes.Clear();
            lblStatus.Text = "Loading...";

            grpEdit.Enabled = false;
            btnApply.Enabled = false;
            lblTargetRace.Text = "No Selection";
            lblTargetRace.ForeColor = Color.Black;

            var designs = new List<DesignEntry>();
            string[] files = Directory.GetFiles(path, "*.json");

            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var node = JsonNode.Parse(json);

                    string name = node?["Name"]?.ToString() ?? Path.GetFileNameWithoutExtension(file);

                    int clan = 0;
                    var clanNode = node?["Customize"]?["Clan"];

                    if (clanNode != null)
                    {
                        if (clanNode["Value"] != null)
                            clan = (int)clanNode["Value"];
                        else
                            try { clan = (int)clanNode; } catch { }
                    }

                    designs.Add(new DesignEntry { Name = name, FilePath = file, ClanId = clan });
                }
                catch { }
            }

            treeViewDesigns.BeginUpdate();
            foreach (var group in designs.GroupBy(d => d.ClanId))
            {
                TreeNode clanNode = new TreeNode(GetClanName(group.Key));
                clanNode.Tag = group.Key;

                foreach (var design in group)
                {
                    TreeNode designNode = new TreeNode(design.Name);
                    designNode.Tag = design;
                    clanNode.Nodes.Add(designNode);
                }
                treeViewDesigns.Nodes.Add(clanNode);
            }
            treeViewDesigns.EndUpdate();
            treeViewDesigns.ExpandAll();

            lblStatus.Text = $"{designs.Count} designs loaded.";
        }

        // --- 2. SELECTION LOGIC ---
        private void treeViewDesigns_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Action == TreeViewAction.Unknown) return;

            if (e.Node.Nodes.Count > 0)
            {
                foreach (TreeNode child in e.Node.Nodes) child.Checked = e.Node.Checked;
            }

            CheckSelectionRules();
        }

        private void CheckSelectionRules()
        {
            var checkedNodes = GetCheckedDesigns();

            if (checkedNodes.Count == 0)
            {
                grpEdit.Enabled = false;
                btnApply.Enabled = false;
                lblStatus.Text = "Select designs...";
                lblTargetRace.Text = "No Selection";
                lblTargetRace.ForeColor = Color.Gray;

                _isLoading = true;
                chkFace.Checked = false; chkHair.Checked = false;
                chkTail.Checked = false; chkPaint.Checked = false;
                _isLoading = false;
                return;
            }

            if (checkedNodes[0].Tag is not DesignEntry first) return;

            int activeClan = first.ClanId;

            bool mixed = checkedNodes.Any(n => n.Tag is DesignEntry d && d.ClanId != activeClan);

            if (mixed)
            {
                lblStatus.Text = "Error: Mixed Clans!";
                lblTargetRace.Text = "ERROR: Mixed Clans!";
                lblTargetRace.ForeColor = Color.Red;
                grpEdit.Enabled = false;
                btnApply.Enabled = false;
                return;
            }

            grpEdit.Enabled = true;
            btnApply.Enabled = true;
            string clanName = GetClanName(activeClan);
            lblStatus.Text = $"{checkedNodes.Count} selected.";

            lblTargetRace.Text = $"Changes will be applied to:\n{clanName}";
            lblTargetRace.ForeColor = Color.DarkBlue;

            UpdateEditFields(activeClan);
            LoadValuesFromFirstDesign(first);
        }

        private void LoadValuesFromFirstDesign(DesignEntry design)
        {
            _isLoading = true;
            try
            {
                chkFace.Checked = false;
                chkHair.Checked = false;
                chkTail.Checked = false;
                chkPaint.Checked = false;

                string jsonText = File.ReadAllText(design.FilePath);
                var root = JsonNode.Parse(jsonText);
                var cust = root?["Customize"];

                if (cust != null)
                {
                    numFace.Value = SafeDecimal(GetIntFromCust(cust, "Face"));
                    numHair.Value = SafeDecimal(GetIntFromCust(cust, "Hairstyle"));

                    int tailVal = GetIntFromCust(cust, "TailShape");
                    if (tailVal == 0) tailVal = GetIntFromCust(cust, "Tail");
                    numTail.Value = SafeDecimal(tailVal);

                    numPaint.Value = SafeDecimal(GetIntFromCust(cust, "FacePaint"));
                }
            }
            catch { }
            finally
            {
                _isLoading = false;
            }
        }

        private decimal SafeDecimal(int val)
        {
            if (val > 255) return 255;
            if (val < 0) return 0;
            return (decimal)val;
        }

        private int GetIntFromCust(JsonNode cust, string key)
        {
            var node = cust[key];
            if (node == null) return 0;
            if (node["Value"] != null) return (int)node["Value"];
            try { return (int)node; } catch { return 0; }
        }

        private void UpdateEditFields(int clanId)
        {
            chkTail.Visible = true; numTail.Visible = true;
            if (clanId == 1 || clanId == 2 || clanId == 3 || clanId == 4 ||
                clanId == 5 || clanId == 6 || clanId == 9 || clanId == 10)
            {
                chkTail.Checked = false;
                chkTail.Visible = false;
                numTail.Visible = false;
            }
        }

        // --- 3. APPLY ---
        private async void btnApply_Click(object sender, EventArgs e)
        {
            var checkedNodes = GetCheckedDesigns();

            if (!chkFace.Checked && !chkHair.Checked && !chkTail.Checked && !chkPaint.Checked)
            {
                MessageBox.Show("No attributes selected!", "Nothing to do");
                return;
            }

            if (MessageBox.Show($"Modify {checkedNodes.Count} files?", "Confirm", MessageBoxButtons.OKCancel) == DialogResult.Cancel)
                return;

            grpEdit.Enabled = false;
            treeViewDesigns.Enabled = false;
            btnApply.Enabled = false;
            progressBar1.Visible = true;
            progressBar1.Maximum = checkedNodes.Count;
            progressBar1.Value = 0;

            await Task.Run(() =>
            {
                foreach (TreeNode node in checkedNodes)
                {
                    if (node.Tag is DesignEntry design)
                    {
                        this.Invoke(new Action(() => lblStatus.Text = $"Writing: {design.Name}"));
                        ApplyChangesToDesign(design);
                        this.Invoke(new Action(() => progressBar1.Value++));
                        System.Threading.Thread.Sleep(10);
                    }
                }
            });

            lblStatus.Text = "Done.";

            if (MessageBox.Show("Changes applied!\nUpdate Cache (Game)?", "Done", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                lblStatus.Text = "Contacting Game...";
                await Task.Delay(1000);
            }

            if (MessageBox.Show("Exit App?", "Finished", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                Application.Exit();
            }
            else
            {
                treeViewDesigns.Enabled = true;
                grpEdit.Enabled = true;
                btnApply.Enabled = true;
                progressBar1.Visible = false;
                lblStatus.Text = "Ready";
                progressBar1.Value = 0;
            }
        }

        private void ApplyChangesToDesign(DesignEntry design)
        {
            try
            {
                string jsonText = File.ReadAllText(design.FilePath);
                JsonNode root = JsonNode.Parse(jsonText);
                if (root == null) return;

                JsonNode customize = root["Customize"];

                if (customize != null)
                {
                    this.Invoke(new Action(() =>
                    {
                        if (chkHair.Checked) WriteValue(customize, "Hairstyle", (int)numHair.Value);
                        if (chkFace.Checked) WriteValue(customize, "Face", (int)numFace.Value);
                        if (chkPaint.Checked) WriteValue(customize, "FacePaint", (int)numPaint.Value);

                        if (chkTail.Checked)
                        {
                            if (customize["Tail"] != null)
                                WriteValue(customize, "Tail", (int)numTail.Value);
                            else
                                WriteValue(customize, "TailShape", (int)numTail.Value);
                        }
                    }));

                    var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(design.FilePath, root.ToJsonString(options));
                }
            }
            catch { }
        }

        private void WriteValue(JsonNode parent, string key, int newValue)
        {
            var node = parent[key];

            if (node == null)
            {
                parent[key] = new JsonObject { ["Value"] = newValue, ["Apply"] = true };
                return;
            }

            if (node["Value"] != null)
            {
                node["Value"] = newValue;
                if (node["Apply"] != null) node["Apply"] = true;
            }
            else
            {
                parent[key] = newValue;
            }
        }

        // --- HELPERS ---
        private List<TreeNode> GetCheckedDesigns()
        {
            var list = new List<TreeNode>();
            AddCheckedNodes(treeViewDesigns.Nodes, list);
            return list;
        }

        private void AddCheckedNodes(TreeNodeCollection nodes, List<TreeNode> list)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Checked && node.Tag is DesignEntry) list.Add(node);
                AddCheckedNodes(node.Nodes, list);
            }
        }

        private string GetClanName(int id)
        {
            switch (id)
            {
                case 1: return "(Hyur) Midlander";
                case 2: return "(Hyur) Highlander";
                case 3: return "(Elezen) Wildwood";
                case 4: return "(Elezen) Duskwight";
                case 5: return "(Lalafell) Plainsfolk";
                case 6: return "(Lalafell) Dunesfolk";
                case 7: return "(Miqo'te) Seeker of the Sun";
                case 8: return "(Miqo'te) Keeper of the Moon";
                case 9: return "(Roegadyn) Sea Wolf";
                case 10: return "(Roegadyn) Hellsguard";
                case 11: return "(Au Ra) Raen";
                case 12: return "(Au Ra) Xaela";
                case 13: return "(Hrothgar) Helions";
                case 14: return "(Hrothgar) The Lost";
                case 15: return "(Viera) Rava";
                case 16: return "(Viera) Veena";
                default: return $"Unknown Clan ({id})";
            }
        }

        // --- DESIGNER CODE ---
        private System.Windows.Forms.Button btnLoad = null!;
        private System.Windows.Forms.TreeView treeViewDesigns = null!;
        private GroupBox grpEdit = null!;
        private CheckBox chkPaint = null!;
        private CheckBox chkTail = null!;
        private CheckBox chkHair = null!;
        private CheckBox chkFace = null!;
        private NumericUpDown numPaint = null!;
        private NumericUpDown numTail = null!;
        private NumericUpDown numHair = null!;
        private NumericUpDown numFace = null!;
        private System.Windows.Forms.Button btnApply = null!;
        private System.Windows.Forms.ProgressBar progressBar1 = null!;
        private Label lblStatus = null!;
        private Label lblTargetRace = null!;

        private void InitializeComponent()
        {
            this.btnLoad = new System.Windows.Forms.Button();
            this.treeViewDesigns = new System.Windows.Forms.TreeView();
            this.grpEdit = new System.Windows.Forms.GroupBox();
            this.numPaint = new System.Windows.Forms.NumericUpDown();
            this.numTail = new System.Windows.Forms.NumericUpDown();
            this.numHair = new System.Windows.Forms.NumericUpDown();
            this.numFace = new System.Windows.Forms.NumericUpDown();
            this.chkPaint = new System.Windows.Forms.CheckBox();
            this.chkTail = new System.Windows.Forms.CheckBox();
            this.chkHair = new System.Windows.Forms.CheckBox();
            this.chkFace = new System.Windows.Forms.CheckBox();
            this.btnApply = new System.Windows.Forms.Button();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblTargetRace = new System.Windows.Forms.Label();
            this.grpEdit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numPaint)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTail)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHair)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFace)).BeginInit();
            this.SuspendLayout();
            // 
            // btnLoad
            // 
            this.btnLoad.Location = new System.Drawing.Point(12, 12);
            this.btnLoad.Name = "btnLoad";
            this.btnLoad.Size = new System.Drawing.Size(292, 23);
            this.btnLoad.TabIndex = 0;
            this.btnLoad.Text = "Load Designs";
            this.btnLoad.UseVisualStyleBackColor = true;
            this.btnLoad.Click += new System.EventHandler(this.btnLoad_Click);
            // 
            // treeViewDesigns
            // 
            this.treeViewDesigns.CheckBoxes = true;
            this.treeViewDesigns.Location = new System.Drawing.Point(12, 41);
            this.treeViewDesigns.Name = "treeViewDesigns";
            this.treeViewDesigns.Size = new System.Drawing.Size(292, 523);
            this.treeViewDesigns.TabIndex = 1;
            this.treeViewDesigns.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.treeViewDesigns_AfterCheck);
            // 
            // grpEdit
            // 
            this.grpEdit.Controls.Add(this.numPaint);
            this.grpEdit.Controls.Add(this.numTail);
            this.grpEdit.Controls.Add(this.numHair);
            this.grpEdit.Controls.Add(this.numFace);
            this.grpEdit.Controls.Add(this.chkPaint);
            this.grpEdit.Controls.Add(this.chkTail);
            this.grpEdit.Controls.Add(this.chkHair);
            this.grpEdit.Controls.Add(this.chkFace);
            this.grpEdit.Enabled = false;
            this.grpEdit.Location = new System.Drawing.Point(329, 41);
            this.grpEdit.Name = "grpEdit";
            this.grpEdit.Size = new System.Drawing.Size(242, 137);
            this.grpEdit.TabIndex = 2;
            this.grpEdit.TabStop = false;
            this.grpEdit.Text = "Batch Settings";
            // 
            // numPaint
            // 
            this.numPaint.Location = new System.Drawing.Point(100, 94);
            this.numPaint.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numPaint.Name = "numPaint";
            this.numPaint.Size = new System.Drawing.Size(120, 23);
            this.numPaint.TabIndex = 7;
            // 
            // numTail
            // 
            this.numTail.Location = new System.Drawing.Point(100, 69);
            this.numTail.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numTail.Name = "numTail";
            this.numTail.Size = new System.Drawing.Size(120, 23);
            this.numTail.TabIndex = 6;
            // 
            // numHair
            // 
            this.numHair.Location = new System.Drawing.Point(100, 44);
            this.numHair.Maximum = new decimal(new int[] {
            2000,
            0,
            0,
            0});
            this.numHair.Name = "numHair";
            this.numHair.Size = new System.Drawing.Size(120, 23);
            this.numHair.TabIndex = 5;
            // 
            // numFace
            // 
            this.numFace.Location = new System.Drawing.Point(100, 19);
            this.numFace.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numFace.Name = "numFace";
            this.numFace.Size = new System.Drawing.Size(120, 23);
            this.numFace.TabIndex = 4;
            // 
            // chkPaint
            // 
            this.chkPaint.AutoSize = true;
            this.chkPaint.Location = new System.Drawing.Point(7, 98);
            this.chkPaint.Name = "chkPaint";
            this.chkPaint.Size = new System.Drawing.Size(91, 19);
            this.chkPaint.TabIndex = 3;
            this.chkPaint.Text = "Facepaint ID";
            this.chkPaint.UseVisualStyleBackColor = true;
            // 
            // chkTail
            // 
            this.chkTail.AutoSize = true;
            this.chkTail.Location = new System.Drawing.Point(7, 73);
            this.chkTail.Name = "chkTail";
            this.chkTail.Size = new System.Drawing.Size(58, 19);
            this.chkTail.TabIndex = 2;
            this.chkTail.Text = "Tail ID";
            this.chkTail.UseVisualStyleBackColor = true;
            // 
            // chkHair
            // 
            this.chkHair.AutoSize = true;
            this.chkHair.Location = new System.Drawing.Point(7, 48);
            this.chkHair.Name = "chkHair";
            this.chkHair.Size = new System.Drawing.Size(62, 19);
            this.chkHair.TabIndex = 1;
            this.chkHair.Text = "Hair ID";
            this.chkHair.UseVisualStyleBackColor = true;
            // 
            // chkFace
            // 
            this.chkFace.AutoSize = true;
            this.chkFace.Location = new System.Drawing.Point(7, 23);
            this.chkFace.Name = "chkFace";
            this.chkFace.Size = new System.Drawing.Size(64, 19);
            this.chkFace.TabIndex = 0;
            this.chkFace.Text = "Face ID";
            this.chkFace.UseVisualStyleBackColor = true;
            // 
            // btnApply
            // 
            this.btnApply.Enabled = false;
            this.btnApply.Location = new System.Drawing.Point(329, 184);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(242, 23);
            this.btnApply.TabIndex = 3;
            this.btnApply.Text = "Apply Changes";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(329, 541);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(834, 23);
            this.progressBar1.TabIndex = 4;
            this.progressBar1.Visible = false;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(329, 523);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(39, 15);
            this.lblStatus.TabIndex = 5;
            this.lblStatus.Text = "Ready";
            // 
            // lblTargetRace
            // 
            this.lblTargetRace.AutoSize = true;
            this.lblTargetRace.Font = new System.Drawing.Font("Segoe UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTargetRace.Location = new System.Drawing.Point(329, 13);
            this.lblTargetRace.Name = "lblTargetRace";
            this.lblTargetRace.Size = new System.Drawing.Size(125, 25);
            this.lblTargetRace.TabIndex = 6;
            this.lblTargetRace.Text = "No Selection";
            // 
            // Form1
            // 
            this.ClientSize = new System.Drawing.Size(1175, 576);
            this.Controls.Add(this.lblTargetRace);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.grpEdit);
            this.Controls.Add(this.treeViewDesigns);
            this.Controls.Add(this.btnLoad);
            this.Name = "Form1";
            this.grpEdit.ResumeLayout(false);
            this.grpEdit.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numPaint)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTail)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHair)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFace)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
    }

    public class DesignEntry
    {
        public string Name { get; set; } = "";
        public string FilePath { get; set; } = "";
        public int ClanId { get; set; } = 0;
    }
}