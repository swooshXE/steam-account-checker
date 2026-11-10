namespace SAC
{
    class Settings
    {
        public static bool showColouredItemsInAccountList = true;

        // Ultra mode: check as fast as Steam answers — no fixed pause between
        // accounts. The adaptive pause still slows the run down when Steam pushes
        // back, so a throttled IP does not simply hammer the endpoint.
        public static bool ultraMode = true;

        // Steam throttles the login endpoint per IP and the block outlives the
        // burst that caused it. With this on, a throttled or timed-out account
        // makes the checker swap the Mullvad relay (new exit IP) and retry instead
        // of sitting out the block. Needs the Mullvad app installed.
        public static bool autoSwitchIp = true;
    }
}
