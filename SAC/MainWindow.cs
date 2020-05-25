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
                try
                {
                    if (!(SteamAccountHelper.filePath == string.Empty))
                    {
                        if (File.Exists(SteamAccountHelper.filePath))
                        {
                            if (!(SteamAccountHelper.fileContent == string.Empty))
                            {
                                if (!(SteamAccountHelper.fileContent.Contains(" ")))
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
                                else
                                    MessageBox.Show("The file contains an invalid character (space) and it may fuck up with checking.\n\nThis will be fixed in a newer version but in the meantime here's what you can do to fix it:\n1. Edit the file you're using by deleting any strings that contains space;\n2. Load the file again and re-check;\n3. (Optional) Throw your PC out the window.", "File contains invalid character", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                                MessageBox.Show("The file you tried to check for doesn't exist or is inaccessible. Please try placing the file in a different location", "File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                            MessageBox.Show("File Path needs to have content in it", "File Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                        MessageBox.Show($"You need to open a file with Steam accounts", "Open a file", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            LogHelper.Log("✔ Done");
        }

        public void ManualCheckBW_DoWork(object sender, DoWorkEventArgs e)
        {
            AccountChecker.CheckManually();
            UIHelper.EnableUI(true);
            UIHelper.ShowUI(false);
            LogHelper.Log("✔ Done");
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