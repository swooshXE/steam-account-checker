using System;
using System.Drawing;
using System.Windows.Forms;

namespace SAC.HelperClasses
{
    class LogHelper
    {
        delegate void SetListAccountsCallback(AccountRow row);
        delegate void SetLogCallback(string output);
        delegate void RefreshListCallback();
        delegate void ClearListCallback();

        public enum Result
        {
            Success,
            SteamGuardProtected,
            Fail,
        }

        public static void Log(string output)
        {
            Label label = Program.mw.labelStatus;

            if (label.InvokeRequired)
            {
                SetLogCallback d = new SetLogCallback(Log);
                label.Invoke(d, new object[] { output });
            }
            else
                label.Text = output;
        }

        public static void ListAccountOnGUI(AccountRow row)
        {
            ListView list = Program.mw.whatsHappening;

            if (list.InvokeRequired)
            {
                SetListAccountsCallback d = new SetListAccountsCallback(ListAccountOnGUI);
                list.Invoke(d, new object[] { row });
            }
            else
            {
                SteamAccountHelper.Rows.Add(row);
                RefreshAccountList();
            }
        }

        // Rebuilds the visible rows from the master list, applying the game filter.
        public static void RefreshAccountList()
        {
            ListView list = Program.mw.whatsHappening;

            if (list.InvokeRequired)
            {
                RefreshListCallback d = new RefreshListCallback(RefreshAccountList);
                list.Invoke(d);
                return;
            }

            list.BeginUpdate();
            try
            {
                list.Items.Clear();

                foreach (AccountRow row in SteamAccountHelper.Rows)
                {
                    if (!SteamAccountHelper.Filter.Matches(row))
                        continue;

                    list.Items.Add(CreateItem(row));
                }
            }
            finally
            {
                list.EndUpdate();
            }
        }

        private static ListViewItem CreateItem(AccountRow row)
        {
            ListViewItem item;
            Color colour;

            switch (row.Result)
            {
                case Result.Success:
                    item = new ListViewItem("✔");
                    colour = row.HasAnyBan ? Color.DarkRed : Color.Green;
                    break;
                case Result.SteamGuardProtected:
                    item = new ListViewItem("🗲");
                    colour = Color.Orange;
                    break;
                case Result.Fail:
                    item = new ListViewItem("✘");
                    colour = Color.Red;
                    break;
                default:
                    item = new ListViewItem("???");
                    row.Detail = $"APPLICATION EXCEPTION. FIX ASAP! -- {row.Detail}";
                    colour = Color.Red;
                    break;
            }

            if (Settings.showColouredItemsInAccountList == true)
                item.ForeColor = colour;

            item.SubItems.Add(row.UserName ?? string.Empty);
            item.SubItems.Add(row.Password ?? string.Empty);
            item.SubItems.Add(row.Detail ?? string.Empty);
            item.SubItems.Add(SteamAccountHelper.Filter.GamesText(row));
            item.SubItems.Add(row.BanInfo ?? "-");

            item.ToolTipText = BuildTooltip(row);

            return item;
        }

        // Everything the row is based on, so a surprising result can be checked
        // against the real profile instead of guessing.
        private static string BuildTooltip(AccountRow row)
        {
            if (row.Result != Result.Success)
                return row.Detail ?? string.Empty;

            string library = SteamAccountHelper.Filter.OwnershipText(row);

            return $"SteamID64: {(row.SteamId == 0 ? "unknown" : row.SteamId.ToString())}\n"
                + $"Ban: {row.BanInfo}\n"
                + $"Games: {library}";
        }

        public static void LogClear()
        {
            SteamAccountHelper.Rows.Clear();

            ListView list = Program.mw.whatsHappening;

            if (list.InvokeRequired)
            {
                ClearListCallback d = new ClearListCallback(LogClear);
                list.Invoke(d);
            }
            else
                list.Items.Clear();
        }
    }
}