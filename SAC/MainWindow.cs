using SAC.HelperClasses;
using SAC.Properties;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace SAC
{
    public partial class MainWindow : Form
    {
        public MainWindow() => InitializeComponent();

        private void MainWindow_Load(object sender, EventArgs e)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            loadingImage.Image = Resources.loading;
            pictureBox1.BackgroundImage = Resources.icon;
            tabControl1.SelectedIndex = 3;

            whatsHappening.Visible = false;
            loadingImage.Visible = false;
            ButtonStart.Enabled = false;
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
                BackgroundWorker automaticWorker = new BackgroundWorker();
                LogHelper.Log("Cleaning up...\n");
                AccountChecker.CleanupUpdate();
                automaticWorker.DoWork += new DoWorkEventHandler(AutomaticCheckBW_DoWork);
                automaticWorker.RunWorkerAsync();
                UIHelper.EnableUI(false);
                UIHelper.ShowUI(true);
            }
            else if (tabControl1.SelectedIndex == 0)
            {
                BackgroundWorker manualWorker = new BackgroundWorker();
                LogHelper.Log("Cleaning up...\n");
                AccountChecker.CleanupUpdate();
                manualWorker.DoWork += new DoWorkEventHandler(ManualCheckBW_DoWork);
                manualWorker.RunWorkerAsync();
                UIHelper.EnableUI(false);
                UIHelper.ShowUI(true);
            }
            else
                MessageBox.Show("Choose 'Check manually' or 'Check automatically' to check for steam accounts", "", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void AutomaticCheckBW_DoWork(object sender, DoWorkEventArgs e)
        {
            AccountChecker.CheckAutomatically();
            UIHelper.EnableUI(true);
            UIHelper.ShowUI(false);
        }

        public void ManualCheckBW_DoWork(object sender, DoWorkEventArgs e)
        {
            AccountChecker.CheckManually();
            UIHelper.EnableUI(true);
            UIHelper.ShowUI(false);
        }

        private void button2_Click(object sender, EventArgs e) => MessageBox.Show("wat is dis?", "wat??", MessageBoxButtons.OK, MessageBoxIcon.Error);

        private void button3_Click(object sender, EventArgs e) => ClipboardHelper.CopyDiscord();

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    InitialDirectory = "C:\\",
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

        private void button7_Click(object sender, EventArgs e) => AccountExporterHelper.Export(AccountExporterHelper.WhatToExport.SGPROTECTEDACCOUNTS);

        private void Button8_Click(object sender, EventArgs e) => WindowsDialogsHelper.FindPlaceToExport();

        private void checkShowPasswordInLogs_CheckedChanged(object sender, EventArgs e)
        {
            if (CheckboxShowColouredItemsInAccountList.Checked == true)
                Settings.showColouredItemsInAccountList = true;
            else
                Settings.showColouredItemsInAccountList = false;
        }

        private void ButtonViewThirdPartyLibraries_Click(object sender, EventArgs e) => Program.vtPLibWindow.ShowDialog();

        private void ButtonViewSoftwareLicense_Click(object sender, EventArgs e) => Program.vSLWindow.ShowDialog();


        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 1)
            {
                whatsHappening.Visible = true;
                ButtonStart.Enabled = true;
            }
            else if (tabControl1.SelectedIndex == 0)
            {
                whatsHappening.Visible = true;
                ButtonStart.Enabled = true;
            }
            else
            {
                whatsHappening.Visible = false;
                ButtonStart.Enabled = false;
            }
        }

        private void textBoxFile_DragOver(object sender, DragEventArgs e)
        {
            //TODO: Add dragover capabilities to the textbox
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