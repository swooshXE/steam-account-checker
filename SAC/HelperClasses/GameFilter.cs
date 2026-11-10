using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAC.HelperClasses
{
    // Steam does not say which game a ban belongs to, so the only provable claim is
    // a pair: the account can run one of the selected games AND carries one of the
    // selected ban flags. Both sides are sets, because an account may be checked
    // against several games and several ban kinds at once.
    class GameFilter
    {
        public const uint ScpSlAppId = 700330;
        public const uint Cs2AppId = 730;
        public const uint CallOfDutyAppId = 1938090;

        public enum BanKind
        {
            Community,
            Game,
            Vac,
            Any,
        }

        // One entry of the games dropdown. Presets carry a readable name; a custom
        // AppID shows as "app 123456" until Steam reports the real title.
        public sealed class GameOption
        {
            public uint AppId { get; }
            public string Name { get; }

            public GameOption(uint appId, string name)
            {
                AppId = appId;
                Name = name ?? string.Empty;
            }

            public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "app " + AppId : Name;
        }

        public static readonly GameOption[] Presets =
        {
            new GameOption(ScpSlAppId, "SCP: SL"),
            new GameOption(Cs2AppId, "Counter-Strike 2"),
            new GameOption(CallOfDutyAppId, "Call of Duty"),
        };

        private readonly object sync = new object();
        private readonly List<GameOption> games = new List<GameOption>();
        private readonly HashSet<BanKind> bans = new HashSet<BanKind>();
        private bool exclude;

        // Shows everything that does NOT match the filter — the accounts that are
        // still usable, when the filter is hunting for banned ones.
        public bool Exclude
        {
            get { lock (sync) return exclude; }
            set { lock (sync) exclude = value; }
        }

        public bool IsActive
        {
            get { lock (sync) return games.Count > 0; }
        }

        public void SetGames(IEnumerable<GameOption> options)
        {
            lock (sync)
            {
                games.Clear();

                if (options == null)
                    return;

                foreach (GameOption option in options)
                {
                    if (option != null && option.AppId != 0 && !games.Exists(g => g.AppId == option.AppId))
                        games.Add(option);
                }
            }
        }

        public GameOption[] SelectedGames()
        {
            lock (sync)
                return games.ToArray();
        }

        public void SetBans(IEnumerable<BanKind> kinds)
        {
            lock (sync)
            {
                bans.Clear();

                if (kinds == null)
                    return;

                foreach (BanKind kind in kinds)
                    bans.Add(kind);
            }
        }

        public BanKind[] SelectedBans()
        {
            lock (sync)
            {
                BanKind[] copy = new BanKind[bans.Count];
                bans.CopyTo(copy);
                return copy;
            }
        }

        public bool Matches(AccountRow row)
        {
            if (!IsActive)
                return true;

            bool matches = MatchesFilter(row);
            return Exclude ? !matches : matches;
        }

        private bool MatchesFilter(AccountRow row)
        {
            if (row == null || row.Result != LogHelper.Result.Success)
                return false;

            if (!OwnsAnySelectedGame(row))
                return false;

            BanCheckerHelper.BanInfo ban = row.Ban;

            if (ban == null || !ban.QuerySucceeded)
                return false;

            // Nothing picked on the ban side means "any ban", which is also what the
            // "Any ban" entry on its own does.
            BanKind[] selected = SelectedBans();

            if (selected.Length == 0)
                return ban.HasAnyBan;

            foreach (BanKind kind in selected)
            {
                switch (kind)
                {
                    case BanKind.Community when ban.CommunityBanned:
                    case BanKind.Game when ban.NumberOfGameBans > 0:
                    case BanKind.Vac when ban.VacBanned:
                    case BanKind.Any when ban.HasAnyBan:
                        return true;
                }
            }

            return false;
        }

        private bool OwnsAnySelectedGame(AccountRow row)
        {
            foreach (GameOption game in SelectedGames())
            {
                if (OwnsGame(row, game.AppId))
                    return true;
            }

            return false;
        }

        // Public view of the ownership check for the exporters: "good accounts that
        // own this game" must use exactly the same rule as the filter.
        public static bool Owns(AccountRow row, uint appId) => OwnsGame(row, appId);

        // The ownership ticket is the authoritative answer: it is issued for bought
        // games AND for games borrowed through Family Sharing, which GetOwnedGames
        // never lists. The library check stays as a fallback for the case where the
        // ticket could not be requested.
        private static bool OwnsGame(AccountRow row, uint appId)
        {
            if (row == null)
                return false;

            bool? ticket = row.TicketAccessFor(appId);

            if (ticket == true)
                return true;

            if (ticket == false)
                return false;

            GameLibrary library = row.Games;
            return library != null && library.Loaded && library.Owns(appId);
        }

        // What the "Games" column shows: with the filter on, the game that made the
        // row visible (with playtime); otherwise the library size.
        public string GamesText(AccountRow row)
        {
            if (row == null || row.Result != LogHelper.Result.Success)
                return "-";

            GameLibrary library = row.Games;
            bool haveLibrary = library != null && library.Loaded;

            if (!IsActive)
                return haveLibrary ? library.Count.ToString() : "?";

            foreach (GameOption game in SelectedGames())
            {
                if (!OwnsGame(row, game.AppId))
                    continue;

                if (haveLibrary && library.Owns(game.AppId))
                    return library.Describe(game.AppId);

                // playable, but not in Steam's owned list: it belongs to another account
                return game.DisplayName + " (shared)";
            }

            return haveLibrary ? library.Count.ToString() : "?";
        }

        // Tooltip line: per selected game, whether the account owns it or borrows it.
        public string OwnershipText(AccountRow row)
        {
            GameOption[] selected = SelectedGames();

            if (selected.Length == 0)
            {
                GameLibrary all = row?.Games;
                return all != null && all.Loaded ? all.Count + " game(s) in library" : "not checked";
            }

            StringBuilder text = new StringBuilder();

            foreach (GameOption game in selected)
            {
                if (text.Length > 0)
                    text.Append("; ");

                text.Append(game.DisplayName).Append(": ");

                if (!OwnsGame(row, game.AppId))
                    text.Append("not found");
                else if (row?.Games != null && row.Games.Loaded && row.Games.Owns(game.AppId))
                    text.Append("owned");
                else
                    text.Append("owned or borrowed");
            }

            return text.ToString();
        }

        // Suggested file name for "Export filtered accounts", so the exported file
        // says what it contains without the user renaming it by hand.
        public string ExportFileName
        {
            get
            {
                GameOption[] selectedGames = SelectedGames();
                BanKind[] selectedBans = SelectedBans();

                string gamesPart = selectedGames.Length == 0 ? "all accounts" : JoinGames(selectedGames);
                string bansPart = selectedBans.Length == 0 ? "any ban" : JoinBans(selectedBans);
                string prefix = Exclude ? "not " : string.Empty;

                return Sanitize($"{gamesPart} - {prefix}{bansPart}.txt");
            }
        }

        private static string JoinGames(GameOption[] selected)
        {
            StringBuilder text = new StringBuilder();

            foreach (GameOption game in selected)
            {
                if (text.Length > 0)
                    text.Append(" + ");

                text.Append(game.DisplayName);
            }

            return text.ToString();
        }

        private static string JoinBans(BanKind[] selected)
        {
            StringBuilder text = new StringBuilder();

            foreach (BanKind kind in selected)
            {
                if (text.Length > 0)
                    text.Append(" + ");

                switch (kind)
                {
                    case BanKind.Community:
                        text.Append("community ban");
                        break;
                    case BanKind.Game:
                        text.Append("game ban");
                        break;
                    case BanKind.Vac:
                        text.Append("VAC ban");
                        break;
                    default:
                        text.Append("any ban");
                        break;
                }
            }

            return text.ToString();
        }

        private static string Sanitize(string fileName)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder clean = new StringBuilder(fileName.Length);

            foreach (char c in fileName)
            {
                if (c == ':' || c == '\\' || c == '/')
                    clean.Append(' ');
                else
                    clean.Append(Array.IndexOf(invalid, c) >= 0 ? ' ' : c);
            }

            // "SCP: SL" must not turn into "SCP  SL"
            while (clean.ToString().Contains("  "))
                clean.Replace("  ", " ");

            return clean.ToString().Trim();
        }
    }
}