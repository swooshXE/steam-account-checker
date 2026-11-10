using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

namespace SAC.HelperClasses
{
    // The games a Steam account owns, as reported by IPlayerService/GetOwnedGames.
    class GameLibrary
    {
        public readonly HashSet<uint> AppIds = new HashSet<uint>();
        public readonly HashSet<string> AppNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Name and playtime per AppID, used to describe the game that matched a filter.
        public readonly Dictionary<uint, string> NameByAppId = new Dictionary<uint, string>();
        public readonly Dictionary<uint, int> PlaytimeMinutesByAppId = new Dictionary<uint, int>();

        // False when Steam did not give us a usable answer (private profile, throttle, timeout).
        public bool Loaded;

        public int Count => AppIds.Count;

        public bool Owns(uint appId) => AppIds.Contains(appId);

        public bool OwnsAny(IEnumerable<uint> appIds)
        {
            foreach (uint appId in appIds)
            {
                if (AppIds.Contains(appId))
                    return true;
            }

            return false;
        }

        public bool HasNameContaining(string fragment)
        {
            foreach (string name in AppNames)
            {
                if (name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        public bool AnyApp(Func<uint, string, bool> predicate)
        {
            return FirstApp(predicate).HasValue;
        }

        public uint? FirstApp(Func<uint, string, bool> predicate)
        {
            foreach (KeyValuePair<uint, string> pair in NameByAppId)
            {
                if (predicate(pair.Key, pair.Value))
                    return pair.Key;
            }

            foreach (uint appId in AppIds)
            {
                if (predicate(appId, string.Empty))
                    return appId;
            }

            return null;
        }

        // "SCP: SL (12h)" — the AppID is the fallback when Steam sent no name.
        public string Describe(uint appId)
        {
            string name;
            if (!NameByAppId.TryGetValue(appId, out name) || string.IsNullOrWhiteSpace(name))
                name = "app " + appId;

            int minutes;
            if (PlaytimeMinutesByAppId.TryGetValue(appId, out minutes) && minutes > 0)
                return $"{name} ({FormatPlaytime(minutes)})";

            return name;
        }

        private static string FormatPlaytime(int minutes)
        {
            if (minutes < 60)
                return minutes + "m";

            int hours = minutes / 60;
            return hours + "h";
        }
    }

    class GameLibraryHelper
    {
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        // The public API hides a library from anonymous callers. Passing the owner's
        // access token together with their SteamID returns the real list, even when
        // the profile itself is private.
        public static GameLibrary Fetch(string accessToken, ulong steamId)
        {
            if (string.IsNullOrEmpty(accessToken) || steamId == 0)
                return null;

            try
            {
                string url = "https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/"
                    + "?access_token=" + Uri.EscapeDataString(accessToken)
                    + "&steamid=" + steamId
                    + "&include_appinfo=1&include_played_free_games=1&include_free_sub=1";

                string body = http.GetStringAsync(url).GetAwaiter().GetResult();

                using JsonDocument doc = JsonDocument.Parse(body);

                if (!doc.RootElement.TryGetProperty("response", out JsonElement response))
                    return null;

                GameLibrary library = new GameLibrary();

                if (response.TryGetProperty("games", out JsonElement games) && games.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement game in games.EnumerateArray())
                    {
                        uint appId = 0;

                        if (game.TryGetProperty("appid", out JsonElement appIdElement)
                            && appIdElement.TryGetInt32(out int appIdNumber)
                            && appIdNumber > 0)
                        {
                            appId = (uint)appIdNumber;
                            library.AppIds.Add(appId);
                        }

                        string name = null;

                        if (game.TryGetProperty("name", out JsonElement nameElement)
                            && nameElement.ValueKind == JsonValueKind.String)
                        {
                            name = nameElement.GetString();
                            if (!string.IsNullOrWhiteSpace(name))
                                library.AppNames.Add(name);
                        }

                        if (appId == 0)
                            continue;

                        if (!string.IsNullOrWhiteSpace(name))
                            library.NameByAppId[appId] = name;

                        if (game.TryGetProperty("playtime_forever", out JsonElement playtimeElement)
                            && playtimeElement.TryGetInt32(out int playtime))
                        {
                            library.PlaytimeMinutesByAppId[appId] = playtime;
                        }
                    }
                }

                library.Loaded = true;
                return library;
            }
            catch
            {
                // a missing library must not turn a verified account into a failed one
                return null;
            }
        }
    }
}