using System;
using System.Windows.Forms;

namespace SAC.HelperClasses
{
    class ClipboardHelper
    {
        public static void CopyDiscord()
        {
            try
            {
                Clipboard.SetText("𝙨𝙬𝙤𝙤𝙨𝙝#1673");
                MessageBox.Show("'𝙨𝙬𝙤𝙤𝙨𝙝#1673' was copied to your clipboard", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed. Error: {ex.Message}", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
