# steam-account-checker

🔥 Check your Steam accounts to see if they are created and SteamGuard protected within seconds 🔥

#### Download it here: https://github.com/swooshXE/steam-account-checker/releases


## Development
SAC is still in a alpha stage and there's still some bugs to fix and some work to do. If you would like to contribute, you can contact me via Discord (𝙨𝙬𝙤𝙤𝙨𝙝#1673) and help the development of SAC.

### Plans for the next releases (in order of priority):
- Task splitting (multi-threading, so you can check multiple accounts at the same time);
- Proxy addition;


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

### Status messages:

- **«nothing»** - Successfully connected to account
- **SteamGuard protected** - The account EXISTS but in order to log in, you must verify it with its associated e-mail address
- **InvalidPassword** - The account doesn't exist (or mispelled something)
- **RateLimitExceeded** - You've checked too many accounts in a short period of time (WE'LL FIX THIS SOON WITH PROXYS!)
- **ServiceUnavailable** - Couldn't connect to Steam servers (it's probably down)
