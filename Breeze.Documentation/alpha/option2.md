# Changing the Breeze Server that your Breeze Wallet uses for Privacy

The uri address used to connect the wallet to a Breeze Server is not editable from the user interface in this release.

```
We do not recommend you change your server if you have already started tumbling coins.
```

If you wish to change it, navigate to your NTumbleBit folder and locate the client.conf and change the value.

The configuration file can be found in the user's home directory at `.ntumblebit/client.conf` or `%appdata%\NTumblebit\client.conf` on Windows.

| OS | NTumbleBit/TestNet/ config parent directory |
| --- | --- |
| Linux                   | /home/\<username\>/.ntumblebit/          |
| Mac OS                  | /Users/\<username\>/.ntumblebit/         |
| Windows Vista, 7, 8, 10 | C:\Users\\<username\>\.ntumblebit\       |
| Windows XP              | C:\Documents and Settings\\<username\>\Application Data\.ntumblebit\ |


You will find the tumbler parameter in client.conf and you can edit it with your favorite text editor:

```
# Tumbler Server 
tumbler.server=ctb://01234567890ABCDEF.onion?h=7a87f8d7vd98s7d898d
```

Quit the client and restart for the change to take effect.  You should see the change in the breeze wallet user interface. 
