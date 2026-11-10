using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SAC.HelperClasses
{
    class AccountExporterHelper
    {
        public enum WhatToExport
        {
            GOODACCOUNTS,
            BADCCOUNTS,
            SGPROTECTEDACCOUNTS,
            VACBANNED
        }

        // Writes exactly what the list currently shows (minus the rows that never
        // logged in) so an inverted filter turns into "every account except these".
        public static void ExportFiltered()
        {
            List<AccountRow> rows = new List<AccountRow>();

            foreach (AccountRow row in SteamAccountHelper.Rows)
            {
                if (row.Result != LogHelper.Result.Success)
                    continue;

                if (!SteamAccountHelper.Filter.Matches(row))
                    continue;

                rows.Add(row);
            }

            if (rows.Count == 0)
            {
                MessageBox.Show("There is nothing to export with the current filter.", "Export filtered accounts", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = SteamAccountHelper.Filter.ExportFileName,
                OverwritePrompt = true,
                RestoreDirectory = true,
            };

            if (Directory.Exists(SteamAccountHelper.localToExport))
                dialog.InitialDirectory = SteamAccountHelper.localToExport;

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                StringBuilder text = new StringBuilder();
                foreach (AccountRow row in rows)
                    text.Append(row.UserName).Append(':').Append(row.Password).Append("\r\n");

                File.WriteAllText(dialog.FileName, text.ToString());

                MessageBox.Show($"Exported {rows.Count} account(s) to:\n{dialog.FileName}", "Export filtered accounts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't write. Error: {ex.Message}", "Export filtered accounts", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // The rows a "clean account" export is built from: the password worked and
        // Steam answered the ban lookup with "no bans at all". A failed lookup means
        // the account cannot be proven clean, so it is left out.
        public static bool IsClean(AccountRow row) =>
            row != null
            && row.Result == LogHelper.Result.Success
            && row.Ban != null
            && row.Ban.QuerySucceeded
            && !row.Ban.HasAnyBan;

        // One-click export of the clean accounts that do NOT have the game: the
        // password works, no ban of any kind, and Steam's ownership answer is known
        // (ticket refused, or the loaded library does not contain the AppID).
        // Ownership is only probed for the games selected in the filter while
        // checking, hence the hint when there is nothing to export.
        public static List<AccountRow> SelectCleanWithoutGame(uint appId)
        {
            List<AccountRow> rows = new List<AccountRow>();

            foreach (AccountRow row in SteamAccountHelper.Rows)
            {
                if (!IsClean(row))
                    continue;

                if (GameFilter.Owns(row, appId))
                    continue;

                if (!OwnershipKnown(row, appId))
                    continue;

                rows.Add(row);
            }

            return rows;
        }

        private static bool OwnershipKnown(AccountRow row, uint appId) =>
            row.TicketAccessFor(appId).HasValue
            || (row.Games != null && row.Games.Loaded);

        public static void ExportCleanWithoutGame(uint appId, string gameName)
        {
            List<AccountRow> rows = SelectCleanWithoutGame(appId);

            if (rows.Count == 0)
            {
                int ownershipUnknown = 0;

                foreach (AccountRow row in SteamAccountHelper.Rows)
                {
                    if (IsClean(row) && !OwnershipKnown(row, appId))
                        ownershipUnknown++;
                }

                string hint = ownershipUnknown > 0
                    ? $"\n\nOwnership of {gameName} was never checked for {ownershipUnknown} account(s): select {gameName} in the Games filter and re-check them."
                    : string.Empty;

                MessageBox.Show($"No clean accounts without {gameName}.{hint}",
                    "Export clean accounts", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string safeName = gameName;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(invalid, '-');

            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"Clean without {safeName} (no ban).txt",
                OverwritePrompt = true,
                RestoreDirectory = true,
            };

            if (Directory.Exists(SteamAccountHelper.localToExport))
                dialog.InitialDirectory = SteamAccountHelper.localToExport;

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                StringBuilder text = new StringBuilder();
                foreach (AccountRow row in rows)
                    text.Append(row.UserName).Append(':').Append(row.Password).Append("\r\n");

                File.WriteAllText(dialog.FileName, text.ToString());

                MessageBox.Show($"Exported {rows.Count} clean account(s) without bans and without {gameName} to:\n{dialog.FileName}",
                    "Export clean accounts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't write. Error: {ex.Message}", "Export clean accounts", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void Export(WhatToExport whatToExport)
        {
            if (!(SteamAccountHelper.localToExport == string.Empty))
            {
                try
                {
                    switch (whatToExport)
                    {
                        case WhatToExport.GOODACCOUNTS:
                            string goodAccounts = string.Join("", SteamAccountHelper.GoodAccountsList.ToArray());
                            StreamWriter streamWriterGoodAccounts = new StreamWriter($"{SteamAccountHelper.localToExport}\\Good Accounts.txt");

                            streamWriterGoodAccounts.WriteLine(goodAccounts);
                            streamWriterGoodAccounts.Close();
                            break;

                        case WhatToExport.BADCCOUNTS:
                            string badAccounts = string.Join("", SteamAccountHelper.badAccountsList.ToArray());
                            StreamWriter streamWriterBadAccounts = new StreamWriter($"{SteamAccountHelper.localToExport}\\Bad Accounts.txt");

                            streamWriterBadAccounts.WriteLine(badAccounts);
                            streamWriterBadAccounts.Close();
                            break;

                        case WhatToExport.SGPROTECTEDACCOUNTS:
                            string sGProtectedAccounts = string.Join("", SteamAccountHelper.sGProtectedAccountsList.ToArray());
                            StreamWriter streamWriterSGAccounts = new StreamWriter($"{SteamAccountHelper.localToExport}\\SteamGuard protected Accounts.txt");

                            streamWriterSGAccounts.WriteLine(sGProtectedAccounts);
                            streamWriterSGAccounts.Close();
                            break;

                        case WhatToExport.VACBANNED:
                            string vacBannedAccounts = string.Join("", SteamAccountHelper.vacBannedAccountsList.ToArray());
                            StreamWriter streamWriterVacAccounts = new StreamWriter($"{SteamAccountHelper.localToExport}\\VAC banned Accounts.txt");

                            streamWriterVacAccounts.WriteLine(vacBannedAccounts);
                            streamWriterVacAccounts.Close();
                            break;
                    }
                    MessageBox.Show("Done!", "SAS.exe", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Couldn't write. Error: {ex.Message}", "SAS.exe", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                WindowsDialogsHelper.FindPlaceToExport();
            }
        }
    }
}