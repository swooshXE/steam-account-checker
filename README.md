## WARNING: This app is only for educational purposes only!
This version will not be updated anymore.

# steam-account-checker v1
🔥 Check your Steam accounts to see if they are created and SteamGuard protected within seconds.

#### Download it here: https://github.com/swooshXE/steam-account-checker/releases

## What can you do:

### Manual check
- Write a Steam username and password and hit 'Check for accounts'

### Automatic check
- Open a file that contains the formatting stated below:
```
username:password
username:password
username:password
username:password
...
```
- Hit 'Check for accounts'

## VAC / game ban check

Every account that logs in successfully is looked up through the Steam Web API
(`ISteamUser/GetPlayerBans`), and the result is shown in the **VAC / bans** column:

- `VAC: 0` - no VAC ban
- `VAC: 2 (431d)` - two VAC bans, the last one 431 days ago
- `VAC: 0 | Game bans: 1` - no VAC ban, but a game ban
- `Community ban` / `Trade ban: probation` - additional restrictions
- `no API key` - no Steam Web API key is configured, so bans are not checked
- `ban query failed` - the request failed (bad key, rate limit, network, ...)

VAC-banned accounts are also highlighted in dark red, counted in the
**VAC banned** field of the statistics box, and can be exported with the
**Export VAC banned...** button (`VAC banned Accounts.txt`).

### API key setup

The Steam Web API requires a (free) key:

1. Log in to https://steamcommunity.com/dev/apikey and register any domain
   (for example `localhost`).
2. Copy the key into **Settings → Steam Web API key**.

The key is stored in `%APPDATA%\SAC\apikey.txt` and is only used for ban lookups.

SteamGuard-protected accounts are rejected before a SteamID is returned, so
their ban status cannot be checked without logging in first.

### Status messages:

- **«nothing»** - Successfully connected to account
- **SteamGuard protected** - The account EXISTS but in order to log in, you must verify it with its associated e-mail address
- **InvalidPassword** - The account doesn't exist (or mispelled something)
- **RateLimitExceeded** - You've checked too many accounts in a short period of time
- **ServiceUnavailable** - Couldn't connect to Steam servers (it's probably down)
