## Install and run TOR
To ensure greater privacy while running the Breeze Wallet we enforce running tor.<br />
Follow the instructions for your operating system below.<br />
Note that you will have to run TOR each time you want to start the wallet.<br />

**Windows**<br />
Download and install the TOR Expert Bundle for Windows at: [torproject.org](https://www.torproject.org/download/download.html.en)<br />
Unzip the Expert Bundle and create a Windows Batch file in the Tor folder, containing:
```
tor -controlport 9051 -cookieauthentication 1
```

Run the batch file (run this each time you want to start the wallet).

**macOS** <br />
Install homebrew
```
/usr/bin/ruby -e "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install)"
```

Install TOR
```
brew install tor
```

Start TOR (run this each time you want to start the wallet)
```
tor -controlport 9051 -cookieauthentication 1
```

**Debian/Ubuntu** <br />
Install TOR
```
sudo apt-get install tor
```

Run TOR (run this each time you want to start the wallet)
```
tor -controlport 9051 -cookieauthentication 1
```