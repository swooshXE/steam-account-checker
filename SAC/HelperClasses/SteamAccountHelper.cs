using SteamKit2;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SAC.HelperClasses
{
    class SteamAccountHelper
    {
        public static readonly List<string> GoodAccountsList = new List<string>();
        public static readonly List<string> sGProtectedAccountsList = new List<string>();
        public static readonly List<string> badAccountsList = new List<string>();

        public static SteamClient steamClient;
        public static CallbackManager manager;
        public static SteamUser steamUser;

        public static string userName, password;

        public static string fileContent = string.Empty;
        public static string filePath = string.Empty;
        public static string localToExport = string.Empty;

        public static int checkedAccounts = 0;
        public static int remainingAccounts = 0;
        public static int maximumData = 0;
        public static int currentValue = 0;
        public static int loggableAccounts = 0;
        public static int steamGuardProtectedAccounts = 0;
        public static int badAccounts = 0;

        public static bool isRunning;

        public static void OnConnected(SteamClient.ConnectedCallback callback)
        {
            LogHelper.Log($"Logging in '{userName}' ('{password}')");
            if (!(callback is null))
                steamUser.LogOn(new SteamUser.LogOnDetails { Username = userName, Password = password });
            else
                MessageBox.Show($"Callback is null", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            if (!(callback is null))
                isRunning = false;
            else
                MessageBox.Show($"Callback is null", "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                if (callback.Result == EResult.AccountLogonDenied)
                {
                    steamGuardProtectedAccounts += 1;
                    sGProtectedAccountsList.Add($"{userName}:{password}\n");
                    steamUser.LogOff();
                    LogHelper.ListAccountOnGUI(LogHelper.Result.SteamGuardProtected, userName, password, "SteamGuard protected");
                    UIHelper.UpdateSGProjectedLabel(steamGuardProtectedAccounts.ToString());
                }
                else
                {
                    badAccounts += 1;
                    badAccountsList.Add($"{userName}:{password}\n");
                    steamUser.LogOff();
                    LogHelper.ListAccountOnGUI(LogHelper.Result.Fail, userName, password, $"{callback.ExtendedResult}");
                    UIHelper.UpdateBadLabel(badAccounts.ToString());
                }
            }
            else
            {
                loggableAccounts += 1;
                GoodAccountsList.Add($"{userName}:{password}\n");
                steamUser.LogOff();
                LogHelper.ListAccountOnGUI(LogHelper.Result.Success, userName, password, string.Empty);
                UIHelper.UpdateLoggableLabel(loggableAccounts.ToString());
            }

            remainingAccounts -= 1;
            checkedAccounts++;
            UIHelper.UpdateRemainingLabel(remainingAccounts.ToString());
            UIHelper.UpdateCheckedLabel(checkedAccounts.ToString());
        }

        public static void OnLoggedOff(SteamUser.LoggedOffCallback callback) => LogHelper.Log($"➜ Closing: {callback.Result}");
    }
}
