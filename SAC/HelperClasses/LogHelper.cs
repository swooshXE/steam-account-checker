using System.Drawing;
using System.Windows.Forms;

namespace SAC.HelperClasses
{
    class LogHelper
    {
        delegate void SetListAccountsCallback(Result result, string userName, string password, string extendedResultMessage);
        delegate void SetLogCallback(string output);

        public enum Result
        {
            Success,
            SteamGuardProtected,
            Fail,
        }

        public static void Log(string output)
        {
            if (Program.mw.InvokeRequired)
            {
                SetLogCallback d = new SetLogCallback(Log);
                Program.mw.labelStatus.Invoke(d, new object[] { output });
            }
            else
                Program.mw.labelStatus.Text = output;
        }

        public static void ListAccountOnGUI(Result result, string userName, string password, string extendedResultMessage)
        {
            if (Program.mw.InvokeRequired)
            {
                SetListAccountsCallback d = new SetListAccountsCallback(ListAccountOnGUI);
                Program.mw.whatsHappening.Invoke(d, new object[] { result, userName, password, extendedResultMessage });
            }
            else
            {
                switch (result)
                {
                    case Result.Success:
                        ListViewItem successItem = new ListViewItem("✔");
                        if (Settings.showColouredItemsInAccountList == true)
                            successItem.ForeColor = Color.Green;
                        successItem.SubItems.Add(userName);
                        successItem.SubItems.Add(password);
                        successItem.SubItems.Add(extendedResultMessage);
                        Program.mw.whatsHappening.Items.Add(successItem);
                        break;
                    case Result.SteamGuardProtected:
                        ListViewItem sgProtectedItem = new ListViewItem("🗲");
                        if (Settings.showColouredItemsInAccountList == true)
                            sgProtectedItem.ForeColor = Color.Orange;
                        sgProtectedItem.SubItems.Add(userName);
                        sgProtectedItem.SubItems.Add(password);
                        sgProtectedItem.SubItems.Add(extendedResultMessage);
                        Program.mw.whatsHappening.Items.Add(sgProtectedItem);
                        break;
                    case Result.Fail:
                        ListViewItem failItem = new ListViewItem("✘");
                        if (Settings.showColouredItemsInAccountList == true)
                            failItem.ForeColor = Color.Red;
                        failItem.SubItems.Add(userName);
                        failItem.SubItems.Add(password);
                        failItem.SubItems.Add(extendedResultMessage);
                        Program.mw.whatsHappening.Items.Add(failItem);
                        break;
                    default:
                        ListViewItem unknownItem = new ListViewItem("???");
                        if (Settings.showColouredItemsInAccountList == true)
                            unknownItem.ForeColor = Color.Red;
                        unknownItem.SubItems.Add(userName);
                        unknownItem.SubItems.Add(password);
                        unknownItem.SubItems.Add($"APPLICATION EXCEPTION. FIX ASAP! -- {extendedResultMessage}");
                        Program.mw.whatsHappening.Items.Add(unknownItem);
                        break;
                }
            }
        }


        public static void LogClear()
        {
            if (!Program.mw.InvokeRequired)
                Program.mw.whatsHappening.Clear();
            else
                MessageBox.Show("jxhdjkxdhjk lmao Invoke required on control 'whatsHappening'", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
