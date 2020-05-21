using System;
using System.Windows.Forms;

namespace SAC.HelperClasses
{
    class WindowsDialogsHelper
    {
        public static void FindPlaceToExport()
        {
            try
            {
                FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();

                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    SteamAccountHelper.localToExport = folderBrowserDialog.SelectedPath;
                    Program.mw.textBoxLocalToExport.Text = SteamAccountHelper.localToExport;
                    //TODO: Change this, so instead of showing this message, it automatically exports without requiring user intervention
                    MessageBox.Show("You've successfully set the place to export accounts from.\n\nClick the button again to export your selected accounts.", "Path is now set", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed. Error: {ex.Message}", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
