using System.Collections.Generic;
using System.Threading;

namespace SAC.HelperClasses
{
    // One checked account. The ListView is rebuilt from these rows whenever the
    // filter changes, so toggling a filter never loses previously found accounts.
    class AccountRow
    {
        public LogHelper.Result Result;
        public string UserName;
        public string Password;
        public string Detail;
        public string BanInfo;
        public BanCheckerHelper.BanInfo Ban;
        public GameLibrary Games;

        // Ownership tickets requested for this account: true — Steam issued a ticket
        // (bought or borrowed through Family Sharing), false — refused, null — not
        // answered. Keyed by AppID, because a run may check several games at once.
        public Dictionary<uint, bool?> TicketAccess;

        public ulong SteamId;

        // null — the ticket was never requested for this AppID
        public bool? TicketAccessFor(uint appId)
        {
            if (TicketAccess == null)
                return null;

            return TicketAccess.TryGetValue(appId, out bool? allowed) ? allowed : null;
        }

        public bool HasAnyBan => Result == LogHelper.Result.Success
            && Ban != null
            && Ban.QuerySucceeded
            && Ban.HasAnyBan;

        public bool HasCommunityBan => Result == LogHelper.Result.Success
            && Ban != null
            && Ban.QuerySucceeded
            && Ban.CommunityBanned;
    }

    class SteamAccountHelper
    {
        public static readonly List<string> GoodAccountsList = new List<string>();
        public static readonly List<string> sGProtectedAccountsList = new List<string>();
        public static readonly List<string> badAccountsList = new List<string>();
        public static readonly List<string> vacBannedAccountsList = new List<string>();

        public static readonly List<AccountRow> Rows = new List<AccountRow>();
        public static readonly GameFilter Filter = new GameFilter();

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
        public static int vacBannedAccounts = 0;

        // The counters and the export lists are written by every worker at once.
        // The counters use Interlocked; the lists are short Add calls guarded by a
        // lock. The lock is never held while a ban lookup or a UI update runs.
        private static readonly object listsLock = new object();

        // The password is verified by Steam's authentication service, so a successful
        // session means the account is usable. The SteamID comes from that same call,
        // which lets us look up the ban status and the owned games without a second logon.
        public static void RecordSuccess(string user, string pass, ulong steamId, GameLibrary games, Dictionary<uint, bool?> ticketAccess)
        {
            int loggable = Interlocked.Increment(ref loggableAccounts);

            lock (listsLock)
                GoodAccountsList.Add($"{user}:{pass}\n");

            BanCheckerHelper.BanInfo banInfo = BanCheckerHelper.GetBanInfo(steamId);

            if (banInfo.QuerySucceeded && banInfo.VacBanned)
            {
                int vac = Interlocked.Increment(ref vacBannedAccounts);

                lock (listsLock)
                    vacBannedAccountsList.Add($"{user}:{pass}\n");

                UIHelper.UpdateVacLabel(vac.ToString());
            }

            LogHelper.ListAccountOnGUI(new AccountRow
            {
                Result = LogHelper.Result.Success,
                UserName = user,
                Password = pass,
                Detail = string.Empty,
                BanInfo = banInfo.ToDisplayString(),
                Ban = banInfo,
                Games = games,
                TicketAccess = ticketAccess,
                SteamId = steamId,
            });

            UIHelper.UpdateLoggableLabel(loggable.ToString());
        }

        public static void RecordSteamGuard(string user, string pass, string detail)
        {
            int guard = Interlocked.Increment(ref steamGuardProtectedAccounts);

            lock (listsLock)
                sGProtectedAccountsList.Add($"{user}:{pass}\n");

            LogHelper.ListAccountOnGUI(new AccountRow
            {
                Result = LogHelper.Result.SteamGuardProtected,
                UserName = user,
                Password = pass,
                Detail = detail,
                BanInfo = "-",
            });

            UIHelper.UpdateSGProjectedLabel(guard.ToString());
        }

        public static void RecordBad(string user, string pass, string detail)
        {
            int bad = Interlocked.Increment(ref badAccounts);

            lock (listsLock)
                badAccountsList.Add($"{user}:{pass}\n");

            LogHelper.ListAccountOnGUI(new AccountRow
            {
                Result = LogHelper.Result.Fail,
                UserName = user,
                Password = pass,
                Detail = detail,
                BanInfo = "-",
            });

            UIHelper.UpdateBadLabel(bad.ToString());
        }
    }
}