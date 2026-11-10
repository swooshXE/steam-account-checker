using SAC.HelperClasses;
using SAC.Properties;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SAC
{
    public partial class MainWindow : Form
    {
        private readonly ToolTip filterTip = new ToolTip();

        public MainWindow() => InitializeComponent();

        private void MainWindow_Load(object sender, EventArgs e)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            loadingImage.Image = Resources.loading;
            pictureBox1.BackgroundImage = Resources.icon;
            tabControl1.SelectedIndex = 3;

            BanCheckerHelper.LoadApiKey();
            textBoxApiKey.Text = BanCheckerHelper.apiKey;

            filterTip.SetToolTip(buttonAddGame, "Add any game by its Steam AppID");
            filterTip.SetToolTip(checkBoxExclude, "Show the opposite: every account that does NOT match the filter");
            filterTip.SetToolTip(buttonGameFilterReset, "Clear the filter and show every checked account");
            filterTip.SetToolTip(buttonExportFiltered, "Write the accounts currently listed to a text file (login:password)");
            filterTip.SetToolTip(ButtonStop, "Stop the running check — the current account is abandoned, results already found are kept");

            gamesFilterDropDown.SetItems(GameFilter.Presets
                .Select(preset => new FilterDropDown.Item(preset.DisplayName, preset)));

            // Community ban by default: that is the flag a game usually shows on a profile.
            bansFilterDropDown.SetItems(new[]
            {
                new FilterDropDown.Item("Community ban", GameFilter.BanKind.Community, true),
                new FilterDropDown.Item("Game ban", GameFilter.BanKind.Game),
                new FilterDropDown.Item("VAC ban", GameFilter.BanKind.Vac),
                new FilterDropDown.Item("Any ban", GameFilter.BanKind.Any),
            });

            whatsHappening.Visible = false;
            gameFilterPanel.Visible = false;
            loadingImage.Visible = false;
            ButtonStart.Enabled = false;

            FixZOrder();
        }

        /// <summary>
        /// The account list and the filter strip are siblings of tabControl1, not
        /// children of it, yet visually they live inside the tab area. The tab
        /// control must stay at the bottom of the z-order, otherwise its page paints
        /// over the list and the accounts "disappear"; the filter buttons get clipped
        /// to a thin strip at the same time. WinForms re-applies the native z-order
        /// every time a sibling is shown, so this has to be re-run on tab changes.
        /// </summary>
        private void FixZOrder()
        {
            tabControl1.SendToBack();
            whatsHappening.BringToFront();
            gameFilterPanel.BringToFront();

            // the status strip used to be drawn over the Stop button when the window
            // was widened; keep the action buttons on top in any case
            flowLayoutPanel1.BringToFront();
            ButtonStart.BringToFront();
            ButtonStop.BringToFront();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            FixZOrder();
        }

        //well this will have to do to fix the lag when resizing............
        protected override void OnResizeBegin(EventArgs e)
        {
            SuspendLayout();
            base.OnResizeBegin(e);
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            ResumeLayout();
            base.OnResizeEnd(e);
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e) => Application.Exit();













        private void ButtonStart_Click(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 1)
            {
                try
                {
                    if (SteamAccountHelper.filePath == string.Empty)
                    {
                        MessageBox.Show($"You need to open a file with Steam accounts", "Open a file", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else if (!File.Exists(SteamAccountHelper.filePath))
                    {
                        MessageBox.Show("The file you tried to check doesn't exist or is inaccessible. Please try placing the file in a different location", "File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else if (SteamAccountHelper.fileContent == string.Empty)
                    {
                        MessageBox.Show("The file has no content. Open it again and make sure it contains lines in the format login:password", "File Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else if (SteamAccountHelper.fileContent.Contains(" "))
                    {
                        MessageBox.Show("The file contains an invalid character (space), which may break the check.\n\nRemove any lines that contain spaces, load the file again and retry.", "File contains invalid character", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        BackgroundWorker automaticWorker = new BackgroundWorker();

                        LogHelper.Log("Cleaning up...\n");
                        AccountChecker.CleanupUpdate();

                        LogHelper.Log("Starting automatic check...\n");
                        automaticWorker.DoWork += new DoWorkEventHandler(AutomaticCheckBW_DoWork);
                        automaticWorker.RunWorkerAsync();

                        UIHelper.EnableUI(false);
                        UIHelper.ShowUI(true);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed. Error: {ex.Message}", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (tabControl1.SelectedIndex == 0)
            {
                if (Program.mw.textBox1.Text == "" | Program.mw.textBox2.Text == "")
                    MessageBox.Show("You need to enter a username and password", "Manual Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                {
                    try
                    {
                        BackgroundWorker manualWorker = new BackgroundWorker();

                        LogHelper.Log("Cleaning up...\n");
                        AccountChecker.CleanupUpdate();

                        LogHelper.Log("Starting manual check...\n");
                        manualWorker.DoWork += new DoWorkEventHandler(ManualCheckBW_DoWork);
                        manualWorker.RunWorkerAsync();

                        UIHelper.EnableUI(false);
                        UIHelper.ShowUI(true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed. Error: {ex.Message}", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            else
                MessageBox.Show("Choose 'Check manually' or 'Check automatically' to check for steam accounts", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void AutomaticCheckBW_DoWork(object sender, DoWorkEventArgs e)
        {
            AccountChecker.CheckAutomatically();
            UIHelper.EnableUI(true);
            UIHelper.ShowUI(false);
            LogHelper.Log(AccountChecker.StopRequested ? "✔ Stopped" : "✔ Done");
        }

        public void ManualCheckBW_DoWork(object sender, DoWorkEventArgs e)
        {
            AccountChecker.CheckManually();
            UIHelper.EnableUI(true);
            UIHelper.ShowUI(false);
            LogHelper.Log(AccountChecker.StopRequested ? "✔ Stopped" : "✔ Done");
        }

        private void ButtonStop_Click(object sender, EventArgs e)
        {
            AccountChecker.RequestStop();
            ButtonStop.Enabled = false;
            LogHelper.Log("⏹ Stopping — waiting for the current account to unwind...");
        }

        private void button3_Click(object sender, EventArgs e) => ClipboardHelper.CopyDiscord();

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FilterIndex = 2,
                    RestoreDirectory = true
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    SteamAccountHelper.filePath = openFileDialog.FileName;
                    textBoxFile.Text = SteamAccountHelper.filePath;

                    var fileStream = openFileDialog.OpenFile();

                    using StreamReader reader = new StreamReader(fileStream);
                    SteamAccountHelper.fileContent = reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed. Error: {ex.Message}", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button5_Click(object sender, EventArgs e) => AccountExporterHelper.Export(AccountExporterHelper.WhatToExport.GOODACCOUNTS);

        private void button6_Click(object sender, EventArgs e) => AccountExporterHelper.Export(AccountExporterHelper.WhatToExport.BADCCOUNTS);

        private void button7_Click(object sender, EventArgs e) => AccountExporterHelper.ExportCleanWithoutGame(GameFilter.ScpSlAppId, "SCP: SL");

        private void button9_Click(object sender, EventArgs e) => AccountExporterHelper.Export(AccountExporterHelper.WhatToExport.VACBANNED);

        private void textBoxApiKey_TextChanged(object sender, EventArgs e) => BanCheckerHelper.SaveApiKey(textBoxApiKey.Text);

        private void Button8_Click(object sender, EventArgs e) => WindowsDialogsHelper.FindPlaceToExport();

        private void checkShowPasswordInLogs_CheckedChanged(object sender, EventArgs e)
        {
            if (CheckboxShowColouredItemsInAccountList.Checked == true)
                Settings.showColouredItemsInAccountList = true;
            else
                Settings.showColouredItemsInAccountList = false;
        }

        private void CheckboxUltraMode_CheckedChanged(object sender, EventArgs e)
        {
            Settings.ultraMode = CheckboxUltraMode.Checked;
        }

        private void CheckboxAutoSwitchIp_CheckedChanged(object sender, EventArgs e)
        {
            Settings.autoSwitchIp = CheckboxAutoSwitchIp.Checked;
        }

        private void ButtonViewThirdPartyLibraries_Click(object sender, EventArgs e) => Program.vtPLibWindow.ShowDialog();

        private void ButtonViewSoftwareLicense_Click(object sender, EventArgs e) => Program.vSLWindow.ShowDialog();


        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool showsAccountList = tabControl1.SelectedIndex == 1 || tabControl1.SelectedIndex == 0;

            whatsHappening.Visible = showsAccountList;
            gameFilterPanel.Visible = showsAccountList;
            ButtonStart.Enabled = showsAccountList;

            // showing a sibling resets the native z-order, the list would end up
            // under the tab page again
            FixZOrder();
        }

        private void GameFilter_Changed(object sender, EventArgs e)
        {
            GameFilter.GameOption[] games = gamesFilterDropDown.CheckedItems
                .Select(item => (GameFilter.GameOption)item.Value)
                .ToArray();

            GameFilter.BanKind[] bans = bansFilterDropDown.CheckedItems
                .Select(item => (GameFilter.BanKind)item.Value)
                .ToArray();

            SteamAccountHelper.Filter.SetBans(bans);
            SteamAccountHelper.Filter.Exclude = checkBoxExclude.Checked;
            SteamAccountHelper.Filter.SetGames(games);

            LogHelper.RefreshAccountList();
        }

        private void buttonAddGame_Click(object sender, EventArgs e)
        {
            uint? appId = FilterDropDown.AskAppId(this);

            if (!appId.HasValue)
                return;

            GameFilter.GameOption preset = GameFilter.Presets
                .FirstOrDefault(option => option.AppId == appId.Value);

            GameFilter.GameOption game = preset ?? new GameFilter.GameOption(appId.Value, string.Empty);

            gamesFilterDropDown.AddItem(game.DisplayName, game, true);
            GameFilter_Changed(sender, e);
        }

        private void GameFilterReset_Click(object sender, EventArgs e)
        {
            checkBoxExclude.Checked = false;
            gamesFilterDropDown.ClearChecks();
            bansFilterDropDown.ClearChecks();
            bansFilterDropDown.Check(GameFilter.BanKind.Community, true);

            // also reset the state when none of the controls actually changed
            GameFilter_Changed(sender, e);
        }

        private void buttonExportFiltered_Click(object sender, EventArgs e) => AccountExporterHelper.ExportFiltered();

        //TODO: Add dragover capabilities to the textbox
        private void textBoxFile_DragOver(object sender, DragEventArgs e)
        {
            //if (e.Data.GetDataPresent(DataFormats.FileDrop))
            //{
            //    string[] fileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            //    SteamAccountHelper.filePath = fileList[0];
            //    textBoxFile.Text = fileList[0];
            //}
            //else
            //    e.Effect = DragDropEffects.None;
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            //Copy username and password of selected
            ListView.SelectedListViewItemCollection selectedItems = whatsHappening.SelectedItems;
            string clipboardText = "";
            foreach (ListViewItem item in selectedItems)
                clipboardText += item.SubItems[1].Text + ":" + item.SubItems[2].Text + "\n";
            if (!(clipboardText == string.Empty))
            {
                Clipboard.SetText(clipboardText);
                MessageBox.Show("Selected username and passwords were copied to the clipboard", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"You have to select one or more items", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            //Copy username of selected
            ListView.SelectedListViewItemCollection selectedItems = whatsHappening.SelectedItems;
            string clipboardText = "";
            foreach (ListViewItem item in selectedItems)
                clipboardText += item.SubItems[1].Text + "\n";
            if (!(clipboardText == string.Empty))
            {
                Clipboard.SetText(clipboardText);
                MessageBox.Show("Selected username were copied to the clipboard", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"You have to select one or more items", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            //Copy password of selected
            ListView.SelectedListViewItemCollection selectedItems = whatsHappening.SelectedItems;
            string clipboardText = "";
            foreach (ListViewItem item in selectedItems)
                clipboardText += item.SubItems[2].Text + "\n";
            if (!(clipboardText == string.Empty))
            {
                Clipboard.SetText(clipboardText);
                MessageBox.Show("Selected passwords were copied to the clipboard", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"You have to select one or more items", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            //Copy all
            ListView.ListViewItemCollection selectedItems = whatsHappening.Items;
            string clipboardText = "";
            foreach (ListViewItem item in selectedItems)
                clipboardText += item.SubItems[1].Text + ":" + item.SubItems[2].Text + "\n";
            if (!(clipboardText == string.Empty))
            {
                Clipboard.SetText(clipboardText);
                MessageBox.Show("All username and passwords were copied to the clipboard", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"You have to select one or more items", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}