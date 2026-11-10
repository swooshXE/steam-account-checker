using System;
using System.Diagnostics;
using System.IO;

namespace SAC.HelperClasses
{
    // Steam throttles the login endpoint per IP. When that happens the whole run
    // can carry on from a different address instead of sitting out the block, and
    // swapping the Mullvad relay is the cheapest way to get one. The work is done
    // by the CLI that ships inside the Mullvad app folder.
    static class MullvadHelper
    {
        // The CLI is not on PATH, and the folder moves between installer builds,
        // so every usual location is tried once.
        private static readonly string[] CliCandidates =
        {
            @"C:\Program Files\Mullvad VPN\resources\mullvad.exe",
            @"C:\Program Files (x86)\Mullvad VPN\resources\mullvad.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Mullvad VPN", "resources", "mullvad.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Mullvad VPN", "resources", "mullvad.exe"),
        };

        private static string cliPath;
        private static bool probed;

        public static string CliPath
        {
            get
            {
                if (!probed)
                {
                    probed = true;

                    foreach (string candidate in CliCandidates)
                    {
                        if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                        {
                            cliPath = candidate;
                            break;
                        }
                    }
                }

                return cliPath;
            }
        }

        public static bool IsInstalled => CliPath != null;

        // The first word of `mullvad status` is the tunnel state:
        // Connected, Disconnected, Connecting or Blocked.
        public static string TunnelState()
        {
            string output = Run(new[] { "status" }, TimeSpan.FromSeconds(10), out _);
            if (string.IsNullOrEmpty(output))
                return null;

            string[] lines = output.TrimStart()
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            return lines.Length > 0 ? lines[0].Trim() : null;
        }

        public static bool IsConnected() => TunnelState() == "Connected";

        // Brings the tunnel up when it is down. Returns true when it is connected
        // by the time the call returns.
        public static bool EnsureConnected(TimeSpan timeout)
        {
            if (!IsInstalled)
                return false;

            if (IsConnected())
                return true;

            Run(new[] { "connect", "-w" }, timeout, out _);
            return IsConnected();
        }

        // Moves to a different relay, which hands the whole machine a new exit IP.
        // When the tunnel is down there is nothing to reconnect, so it is brought
        // up instead.
        //
        // A location pinned to one city or one server reconnects to the same relay
        // and would leave the blocked IP in place, so when the relay did not change
        // the constraint is widened to the whole country of the current relay —
        // that is where a random server is picked on every connect.
        public static bool Rotate(TimeSpan timeout)
        {
            if (!IsInstalled)
                return false;

            if (!IsConnected())
            {
                Run(new[] { "connect", "-w" }, timeout, out _);
                return IsConnected();
            }

            string before = RelayHostname();

            Run(new[] { "reconnect", "-w" }, timeout, out _);

            if (!IsConnected())
                return false;

            if (string.Equals(before, RelayHostname(), StringComparison.OrdinalIgnoreCase))
            {
                string country = CountryOf(before);

                if (country != null)
                {
                    Run(new[] { "relay", "set", "location", country }, timeout, out _);
                    Run(new[] { "reconnect", "-w" }, timeout, out _);
                }
            }

            // The CLI only returns once the new relay is up, but the daemon can
            // still report "Connecting" for a moment; one more look settles it.
            return IsConnected();
        }

        // The relay line of `mullvad status`, for example "gb-lon-wg-306".
        private static string RelayHostname()
        {
            string output = Run(new[] { "status" }, TimeSpan.FromSeconds(10), out _);
            if (string.IsNullOrEmpty(output))
                return null;

            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                int at = trimmed.IndexOf("Relay:", StringComparison.OrdinalIgnoreCase);

                if (at >= 0)
                    return trimmed.Substring(at + "Relay:".Length).Trim();
            }

            return null;
        }

        // "gb-lon-wg-306" -> "gb"; the relay hostname already carries the country.
        private static string CountryOf(string hostname)
        {
            if (string.IsNullOrEmpty(hostname))
                return null;

            int dash = hostname.IndexOf('-');
            return dash > 0 ? hostname.Substring(0, dash) : null;
        }

        // Runs the CLI and returns its combined output. Reading is done on
        // background tasks so a quiet process cannot fill a pipe and stall the
        // timeout, and the process is killed when the timeout runs out.
        private static string Run(string[] args, TimeSpan timeout, out bool ok)
        {
            ok = false;

            string cli = CliPath;
            if (cli == null)
                return null;

            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = cli,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                foreach (string arg in args)
                    info.ArgumentList.Add(arg);

                using (Process process = Process.Start(info))
                {
                    if (process == null)
                        return null;

                    System.Threading.Tasks.Task<string> output = process.StandardOutput.ReadToEndAsync();
                    System.Threading.Tasks.Task<string> error = process.StandardError.ReadToEndAsync();

                    if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                    {
                        try { process.Kill(true); } catch { /* already gone */ }
                        return null;
                    }

                    ok = process.ExitCode == 0;

                    return output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
