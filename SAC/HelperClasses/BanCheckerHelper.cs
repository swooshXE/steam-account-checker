using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;

namespace SAC.HelperClasses
{
    class BanCheckerHelper
    {
        public class BanInfo
        {
            public bool QuerySucceeded;
            public string ErrorMessage = string.Empty;
            public bool VacBanned;
            public int NumberOfVacBans;
            public int DaysSinceLastBan;
            public int NumberOfGameBans;
            public bool CommunityBanned;
            public string EconomyBan = string.Empty;

            public bool HasAnyBan
            {
                get
                {
                    return VacBanned
                        || NumberOfGameBans > 0
                        || CommunityBanned
                        || (!string.IsNullOrEmpty(EconomyBan) && !EconomyBan.Equals("none", StringComparison.OrdinalIgnoreCase));
                }
            }

            public string ToDisplayString()
            {
                if (!QuerySucceeded)
                    return ErrorMessage;

                List<string> parts = new List<string>();

                if (VacBanned)
                    parts.Add($"VAC {NumberOfVacBans}");

                if (NumberOfGameBans > 0)
                    parts.Add($"Game ban {NumberOfGameBans}");

                if (CommunityBanned)
                    parts.Add("Community ban");

                if (!string.IsNullOrEmpty(EconomyBan) && !EconomyBan.Equals("none", StringComparison.OrdinalIgnoreCase))
                    parts.Add($"Trade ban: {EconomyBan}");

                if (HasAnyBan && DaysSinceLastBan > 0)
                    parts.Add($"{DaysSinceLastBan}d ago");

                return parts.Count == 0 ? "clean" : string.Join(" | ", parts);
            }
        }

        public static string apiKey = string.Empty;

        private static readonly Dictionary<ulong, BanInfo> cache = new Dictionary<ulong, BanInfo>();
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SAC");
        private static readonly string apiKeyFile = Path.Combine(settingsFolder, "apikey.txt");

        public static bool HasApiKey => !string.IsNullOrWhiteSpace(apiKey);

        public static void LoadApiKey()
        {
            try
            {
                apiKey = File.Exists(apiKeyFile) ? File.ReadAllText(apiKeyFile).Trim() : string.Empty;
            }
            catch (Exception ex)
            {
                apiKey = string.Empty;
                LogHelper.Log($"⚠ Couldn't load the saved API key: {ex.Message}\n");
            }
        }

        public static void SaveApiKey(string newApiKey)
        {
            newApiKey = (newApiKey ?? string.Empty).Trim();

            if (newApiKey == apiKey)
                return;

            try
            {
                Directory.CreateDirectory(settingsFolder);
                File.WriteAllText(apiKeyFile, newApiKey);
                apiKey = newApiKey;
                lock (cache)
                    cache.Clear(); // previous results may have been produced with another key
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Couldn't save the API key. Error: {ex.Message}", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static BanInfo GetBanInfo(ulong steamId)
        {
            // several workers may ask at the same time; a lookup that is already
            // running for the same account is cheap to repeat, but the dictionary
            // itself must not be touched from two threads
            lock (cache)
            {
                if (cache.TryGetValue(steamId, out BanInfo cachedInfo))
                    return cachedInfo;
            }

            BanInfo info = Query(steamId);

            // cache successes only — a failed lookup should be retried next time
            if (info.QuerySucceeded)
            {
                lock (cache)
                    cache[steamId] = info;
            }

            return info;
        }

        private static BanInfo Query(ulong steamId)
        {
            BanInfo info = new BanInfo();

            if (!HasApiKey)
            {
                info.ErrorMessage = "no API key";
                return info;
            }

            if (steamId == 0)
            {
                info.ErrorMessage = "no SteamID";
                return info;
            }

            string requestUrl = "https://api.steampowered.com/ISteamUser/GetPlayerBans/v1/"
                + "?key=" + Uri.EscapeDataString(apiKey)
                + "&steamids=" + steamId.ToString();

            try
            {
                using (HttpResponseMessage response = httpClient.GetAsync(requestUrl).GetAwaiter().GetResult())
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        info.ErrorMessage = $"ban query failed (HTTP {(int)response.StatusCode})";
                        LogHelper.Log($"⚠ Couldn't query the ban status of {steamId}: HTTP {(int)response.StatusCode} ({response.ReasonPhrase})\n");
                        return info;
                    }

                    using (Stream stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (JsonDocument document = JsonDocument.Parse(stream))
                    {
                        // GetPlayerBans returns {"players":[...]}; some endpoints wrap it
                        // in {"response":{"players":[...]}}, so accept both shapes.
                        JsonElement players;
                        if (!document.RootElement.TryGetProperty("players", out players))
                        {
                            JsonElement responseBody;
                            if (!document.RootElement.TryGetProperty("response", out responseBody)
                                || !responseBody.TryGetProperty("players", out players))
                            {
                                info.ErrorMessage = "unexpected API response";
                                LogHelper.Log("⚠ Ban query returned an unexpected response\n");
                                return info;
                            }
                        }

                        if (players.GetArrayLength() == 0)
                        {
                            info.ErrorMessage = "no ban data";
                            return info;
                        }

                        FillFromPlayer(players[0], info);
                        info.QuerySucceeded = true;
                    }
                }
            }
            catch (Exception ex)
            {
                info.QuerySucceeded = false;
                info.ErrorMessage = "ban query failed";
                LogHelper.Log($"⚠ Couldn't query the ban status of {steamId}: {ex.Message}\n");
            }

            return info;
        }

        internal static void FillFromPlayer(JsonElement player, BanInfo info)
        {
            info.VacBanned = GetBoolean(player, "VACBanned");
            info.NumberOfVacBans = GetInt32(player, "NumberOfVACBans");
            info.DaysSinceLastBan = GetInt32(player, "DaysSinceLastBan");
            info.NumberOfGameBans = GetInt32(player, "NumberOfGameBans");
            info.CommunityBanned = GetBoolean(player, "CommunityBanned");

            JsonElement economyBan;
            info.EconomyBan = player.TryGetProperty("EconomyBan", out economyBan) && economyBan.ValueKind == JsonValueKind.String
                ? economyBan.GetString() ?? "none"
                : "none";
        }

        private static bool GetBoolean(JsonElement element, string propertyName)
        {
            JsonElement value;
            return element.TryGetProperty(propertyName, out value) && value.ValueKind == JsonValueKind.True;
        }

        private static int GetInt32(JsonElement element, string propertyName)
        {
            JsonElement value;
            int number;
            return element.TryGetProperty(propertyName, out value) && value.TryGetInt32(out number) ? number : 0;
        }
    }
}
