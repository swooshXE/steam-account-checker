using SAC.HelperClasses;
using SteamKit2;
using System;
using System.IO;
using System.Windows.Forms;

namespace SAC
{
    class AccountChecker
    {
        private static int firstUsername = 0; //Do not change
        private static int firstPassword = 1; //Do not change

        delegate void SetVisibilityCallback(bool isVisible);

        public static void CheckManually()
        {
            if (Program.mw.textBox1.Text == "" | Program.mw.textBox2.Text == "")
                MessageBox.Show("You need to enter a username and password", "Manual Check", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
            {
                try
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
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed. Error: {ex.Message}", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public static void CheckAutomatically()
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
                                LogHelper.Log("Starting automatic check...\n");
                                SteamAccountHelper.filePath = Program.mw.textBoxFile.Text;
                                SteamAccountHelper.maximumData = SteamAccountHelper.fileContent.Split(new[] { ':', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length / 2;
                                SteamAccountHelper.remainingAccounts = SteamAccountHelper.maximumData;

                                UIHelper.UpdateRemainingLabel(SteamAccountHelper.remainingAccounts.ToString());

                                while (SteamAccountHelper.currentValue < SteamAccountHelper.maximumData)
                                {
                                    SteamAccountHelper.currentValue++;
                                    try
                                    {
                                        string[] splittedInformation = SteamAccountHelper.fileContent.Split(new[] { ':', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                        string[] anotherSplittedInformation = splittedInformation;

                                        SteamAccountHelper.steamClient = new SteamClient();
                                        SteamAccountHelper.manager = new CallbackManager(SteamAccountHelper.steamClient);

                                        SteamAccountHelper.userName = anotherSplittedInformation[firstUsername];
                                        SteamAccountHelper.password = anotherSplittedInformation[firstPassword];

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
                                LogHelper.Log("✔ Done");
                            }
                            else
                                MessageBox.Show("The file contains an invalid character (space) and it may fuck up with checking.\n\nThis will be fixed in a newer version but in the meantime here's what you can do to fix it:\n1. Close the program;\n2. Edit the file you're using by deleting any strings that contains space;\n3. Open the program, re-upload the files and start checking again;\n4. (Optional) Throw your PC out the window.", "File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                            MessageBox.Show("The file you tried to check for doesn't exist or is inaccessible. Please try placing the file in a different location", "File", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                        MessageBox.Show("File Path needs to have content in it", "File Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed. Error: {ex.Message}", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        public static void CleanupUpdate() //Cleanup and reset some variables so we re-check
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
