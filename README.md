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

### Status messages:

- **«nothing»** - Successfully connected to account
- **SteamGuard protected** - The account EXISTS but in order to log in, you must verify it with its associated e-mail address
- **InvalidPassword** - The account doesn't exist (or mispelled something)
- **RateLimitExceeded** - You've checked too many accounts in a short period of time - Adding proxys in V2
- **ServiceUnavailable** - Couldn't connect to Steam servers (it's probably down)
