using SAC.HelperClasses;
using SteamKit2;
using System;
using System.Windows.Forms;

namespace SAC
{
    class AccountChecker
    {
        private static int firstUsername = 0; //Do not change - gets >username<  :password
        private static int firstPassword = 1; //Do not change - gets username:  >password<

        delegate void SetVisibilityCallback(bool isVisible);

        public static void CheckManually()
        {
            LogHelper.Log("⌛ Starting manual check...\n");
            SteamAccountHelper.userName = Program.mw.textBox1.Text;
            SteamAccountHelper.password = Program.mw.textBox2.Text;

            SteamAccountHelper.steamClient = new SteamClient();

            SteamAccountHelper.manager = new CallbackManager(SteamAccountHelper.steamClient);

            SteamAccountHelper.steamUser = SteamAccountHelper.steamClient.GetHandler<SteamUser>();

            SteamAccountHelper.manager.Subscribe<SteamClient.ConnectedCallback>(SteamAccountHelper.OnConnected);
            SteamAccountHelper.manager.Subscribe<SteamClient.DisconnectedCallback>(SteamAccountHelper.OnDisconnected);
            SteamAccountHelper.manager.Subscribe<SteamUser.LoggedOnCallback>(SteamAccountHelper.OnLoggedOn);
            SteamAccountHelper.manager.Subscribe<SteamUser.LoggedOffCallback>(SteamAccountHelper.OnLoggedOff);

            SteamAccountHelper.isRunning = true;

            SteamAccountHelper.steamClient.Connect();

            while (SteamAccountHelper.isRunning)
            {
                SteamAccountHelper.manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        public static void CheckAutomatically()
        {
            SteamAccountHelper.filePath = Program.mw.textBoxFile.Text;
            SteamAccountHelper.maximumData = SteamAccountHelper.fileContent.Split(new[] { ':', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length / 2;
            SteamAccountHelper.remainingAccounts = SteamAccountHelper.maximumData;

            UIHelper.UpdateRemainingLabel(SteamAccountHelper.remainingAccounts.ToString());

            while (SteamAccountHelper.currentValue < SteamAccountHelper.maximumData)
            {
                SteamAccountHelper.currentValue++;
                try
                {
                    string[] splitUsersPasswords = SteamAccountHelper.fileContent.Split(new[] { ':', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    SteamAccountHelper.steamClient = new SteamClient();
                    SteamAccountHelper.manager = new CallbackManager(SteamAccountHelper.steamClient);

                    SteamAccountHelper.userName = splitUsersPasswords[firstUsername];
                    SteamAccountHelper.password = splitUsersPasswords[firstPassword];

                    SteamAccountHelper.steamUser = SteamAccountHelper.steamClient.GetHandler<SteamUser>();

                    SteamAccountHelper.manager.Subscribe<SteamClient.ConnectedCallback>(SteamAccountHelper.OnConnected);
                    SteamAccountHelper.manager.Subscribe<SteamClient.DisconnectedCallback>(SteamAccountHelper.OnDisconnected);
                    SteamAccountHelper.manager.Subscribe<SteamUser.LoggedOnCallback>(SteamAccountHelper.OnLoggedOn);
                    SteamAccountHelper.manager.Subscribe<SteamUser.LoggedOffCallback>(SteamAccountHelper.OnLoggedOff);

                    SteamAccountHelper.isRunning = true;

                    SteamAccountHelper.steamClient.Connect();

                    while (SteamAccountHelper.isRunning)
                    {
                        SteamAccountHelper.manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
                    }

                    firstUsername += 2;
                    firstPassword += 2;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed. Error: {ex.Message}", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }


        public static void CleanupUpdate() // Cleans up any left-overs from the last session to avoid conflict and errors
        {
            SteamAccountHelper.userName = string.Empty;
            SteamAccountHelper.password = string.Empty;
            SteamAccountHelper.localToExport = string.Empty;
            SteamAccountHelper.checkedAccounts = 0;
            SteamAccountHelper.remainingAccounts = 0;
            SteamAccountHelper.maximumData = 0;
            SteamAccountHelper.currentValue = 0;
            SteamAccountHelper.loggableAccounts = 0;
            SteamAccountHelper.steamGuardProtectedAccounts = 0;
            SteamAccountHelper.badAccounts = 0;

            UIHelper.UpdateLoggableLabel(SteamAccountHelper.loggableAccounts.ToString());
            UIHelper.UpdateSGProjectedLabel(SteamAccountHelper.steamGuardProtectedAccounts.ToString());
            UIHelper.UpdateBadLabel(SteamAccountHelper.badAccounts.ToString());

            firstUsername = 0; //Do not change
            firstPassword = 1; //Do not change
        }

    }
}