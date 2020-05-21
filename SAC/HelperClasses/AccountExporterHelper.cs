using System;
using System.IO;
using System.Windows.Forms;

namespace SAC.HelperClasses
{
    class AccountExporterHelper
    {
        public enum WhatToExport
        {
            GOODACCOUNTS,
            BADCCOUNTS,
            SGPROTECTEDACCOUNTS
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