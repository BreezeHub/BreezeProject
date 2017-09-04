| Windows | Linux | OS X |
| :---- | :---- | :---- |
[![Windows build status][1]][2] | [![Linux build status][3]][4] | [![OS X build status][5]][6] |

[1]: https://ci.appveyor.com/api/projects/status/2lcwh99pph77qer2?svg=true
[2]: https://ci.appveyor.com/project/BreezeHubAdmin/breezeserver/branch/master
[3]: https://travis-ci.org/BreezeHub/BreezeServer.svg?branch=master
[4]: https://travis-ci.org/BreezeHub/BreezeServer
[5]: https://travis-ci.org/BreezeHub/BreezeServer.svg?branch=master
[6]: https://travis-ci.org/BreezeHub/BreezeServer

# BreezeServer

BreezeServer is a service host for our implementation of [TumbleBit](http://tumblebit.cash) in .NET Core. It is an trustless bitcoin-compatible anonymous payments protocol.

## The BreezeServer Alpha Release
This release includes the following:

###### Node Advertisment Protocol

  A high level overview of the protocol *operations* performed by each Breeze TumbleBit Server is as follows:
  1. The node operator starts up the BreezeServer software.
  2. The node checks to see if it has already registered itself on the Stratis blockchain.
  3. If it has registered, the tumbler service is initialized as normal.
  4. If the node has not yet registered, or if its configuration has changed, the registration transaction updates and it broadcasts again.
  5. Once registered the service is ready for connections from BreezeClients such as BreezeWallet.

###### Registration Transaction

  The registration transaction is a specially-formatted transaction broadcast by the Breeze Server to the Stratis network. In this release, the registration transactions are broadcast to the main Stratis blockchain.

###### Security Features

  The registration transaction carries the following information:
  1. The IP address of the Breeze Server
  2. (Currently optional) TOR address of the server
  3. The port that wallets should use to connect.
  4. All the information is signed by the tumbler’s private keys. This means that the signatures can be validated by a Breeze wallet when it connects to the Breeze Server. The registration protocol will greatly benefit from widespread testing by the Stratis community.

As this is alpha software, the tumbler is currently configured to only operate on the Bitcoin testnet. This is to prevent loss of funds in the event of errors. Once the tumbler is sufficiently stable, a Bitcoin mainnet version will be released.

## How to Run

#### Prerequisites:

As a user, you will need:
  - [.NET Core 1.1.2 SDK 1.0.4](https://github.com/dotnet/core/blob/master/release-notes/download-archives/1.1.2-download.md) which is available for Windows, Mac OS and several Linux distributions (RHEL, Ubuntu, Debian, Fedora, CentOS, SUSE).
  - [StratisD](https://github.com/stratisproject/StratisX) fully synced, rpc enabled
  - [Bitcoin Core 0.13.1](https://bitcoin.org/bin/bitcoin-core-0.13.1/) or later.  Fully sync'd, rpc enabled.

#### Install .Net Core SDK:

More information about installing .NET Core on your system can be found [here](https://www.microsoft.com/net/core).  If you are a developer or want to browse the code, you may like to install one of the following:

  - [Visual Studio Code](https://code.visualstudio.com/) with [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp) (cross platform), or
  - [Visual Studio 2017](https://www.visualstudio.com/downloads/) (Windows and Mac OS)

#### Install Stratis X (and configure StratisX to use the stratis testnet blockchain)

##### Windows and Mac

Packages for Windows and Mac OS X can be found [here](https://github.com/stratisproject/stratisX/releases/tag/v2.0.0.3).

Run the installed *stratisqt* in testnet mode and let it sync the testnet blockchain transaction.

```
stratis-qt -testnet
```

This will create a folder on your computer named Stratis in the following location:

```
%AppData%\Roaming\Stratis
```

Close the software, create a ```stratis.conf``` file in this folder and set it up a follows:

```
# ~/.stratis/stratis.conf

# Run on the test network instead of the real bitcoin network.
testnet=1

# reindex the blockchain
txindex=1

#run in the background
daemon=1

# RPC user and password. We don't yet support cookie authentication
rpcuser=stratisuser
rpcpassword=stratispassword

#accept json rpc commands
server=1

# Optional: pruning can reduce the disk usage
prune=2000
```
Run again. This time you need not specify explicitly the testnet flag, because you already set it in the config. You can send it some testcoins.

##### Linux

On linux we currently recommend you install and run stratisd v2.0.0.3.

###### Step 1: Create a user for running stratisd (optional)
This step is optional, but for better security and resource separation we suggest you create a separate user just for running stratisd. We will also use the ~/bin directory to keep locally installed files (others might want to use /usr/local/bin instead). We will download source code files to the ~/src directory. (Example here is for linux and was tested on Ubuntu).

Enter the following at your terminal:
```
sudo adduser stratis --disabled-password
sudo apt-get install git
sudo su - stratis
mkdir ~/bin ~/src
echo $PATH
```
If you don't see /home/stratis/bin in the output, you should add this line to your .bashrc, .profile, or .bash_profile, then logout and relogin: 
```
PATH=$HOME/bin:$PATH
```

Leave the stratis user at your shell:
```
exit
```

###### Step 2: Download StratisD

Here are some pointers for ubuntu:
```
sudo apt-get install libminiupnpc-dev libdb++-dev libdb-dev libcrypto++-dev libqrencode-dev libboost-all-dev build-essential libboost-system-dev libboost-filesystem-dev libboost-program-options-dev libboost-thread-dev libboost-filesystem-dev libboost-program-options-dev libboost-thread-dev libssl-dev libdb++-dev libssl-dev ufw git                                                                                                        sudo add-apt-repository -y ppa:bitcoin/bitcoin                                                                                                             
sudo apt-get update                                                                                                                                        
sudo apt-get install -y libdb4.8-dev libdb4.8++-dev                                                                                                        
sudo su - stratis                                                                                                                                          
cd ~/src && git clone https://github.com/stratisproject/stratisX.git                                                                                       
cd stratis/src                                                                                                                                             
make -f makefile.unix # This will error if you don't have all dependencies listed in StratisX/doc/build-unix.txt                                                                                                                                     
cp -a stratisd ~/bin   
```

###### Step 3: Configure & run StratisD on testnet

Create/edit your stratis.conf:
```
mkdir ~/.stratis
$EDITOR ~/.stratis/stratis.conf
```
Write this in stratis.conf:
```
# ~/.stratis/stratis.conf

# Run on the test network instead of the real bitcoin network.
testnet=1

# reindex the blockchain
txindex=1

#run in the background
daemon=1

# RPC user and password. We don't yet support cookie authentication
rpcuser=stratisuser
rpcpassword=stratispassword

#accept json rpc commands
server=1

# Optional: pruning can reduce the disk usage
prune=2000
```

Finally, boot up stratisd, let it sync with the network, and send it some coins.

If you have an existing installation of stratisd and have not previously set txindex=1 you need to reindex the blockchain by running:
```
stratisd -reindex
```

If you already have a freshly indexed copy of the blockchain with txindex run stratisd with:
```
stratisd
```

Allow some time to pass so stratisd connects to the network and starts downloading blocks. You can check its progress by running
```
stratisd getinfo
```

#### Configuring Bitcoin Core for testnet

[Download](https://bitcoin.org/en/bitcoin-core/) Bitcoin Core.

Create/edit your bitcoin.conf:
```
# ~/.bitcoin/bitcoin.conf
# run on testnet
testnet=1

# server=1 tells Bitcoin-Qt and bitcoind to accept JSON-RPC commands
server=1

# Enable pruning to reduce storage requirements by deleting old blocks. 
prune=2000

rpcuser=bitcoinuser
rpcpassword=bitcoinpassword
```

Place the file in the relevant directory based on your system:

| OS | bitcoin.conf parent directory |
| --- | --- |
| Linux                   | /home/\<username\>/.bitcoin/                                     |
| Mac OS                  | /Users/\<username\>/Library/Application Support/Bitcoin/         |
| Windows Vista, 7, 8, 10 | C:\Users\\<username\>\AppData\Roaming\Bitcoin\                   |
| Windows XP              | C:\Documents and Settings\\<username\>\Application Data\Bitcoin\ |

Finally, boot up bitcoind or bitcoin-qt, let it sync with the network, and send it some coin.


#### Getting the Code

Navigate to where you would like to save the code in your shell and then:
```
git clone http://www.github.com/BreezeHub/BreezeServer.git --recursive
```
The `--recursive` command is vital because BreezeServer relies on git submodules.

In the newly created Breeze.Server folder run the following command:

```dotnet restore```

The `dotnet restore` command uses NuGet to restore dependencies as well as project-specific tools that are specified in the project file.

#### Running Tests (optional)

```
cd Breeze.BreezeServer.Tests/
dotnet test
```
Your results should be something like this:
```
Starting test execution, please wait...
[xUnit.net 00:00:01.1439914]   Discovering: Breeze.BreezeServer.Tests
[xUnit.net 00:00:01.4319561]   Discovered:  Breeze.BreezeServer.Tests
[xUnit.net 00:00:01.5379946]   Starting:    Breeze.BreezeServer.Tests
[xUnit.net 00:00:02.3485939]   Finished:    Breeze.BreezeServer.Tests

Total tests: 2. Passed: 2. Failed: 0. Skipped: 0.
Test Run Successful.
Test execution time: 4.5518 Seconds
```

##### Setup Tor

The server requires Tor Hidden Services as part of the privacy protocol.  Download Tor [here](https://www.torproject.org/download/download) and run it with the following command.

```
tor -controlport 9051 -cookieauthentication 1
```
#### Configure & Run

##### Notes before you start
- Your wallet is only as secure as your configuration files, so apply appropriate permissions.
- This is pre-release alpha software. You should only be working testnet.

##### Server
After installing .NET Core, launching stratisd or stratisqt on testnet.  Run the server and an empty configuration file will be generated for you.

```
cd Breeze.BreezeServer
dotnet run -testnet
```

##### Configuring `breeze.conf`
Now we're ready to set up `breeze.conf`. Edit the contents to look something like this.  The minimum config to get a working version is shown:

```
# ~/.breezeserver/breeze.conf
# %AppData%\Roaming\breeze.conf

# we must work on testnet for the alpha
testnet=1

#these settings are the RPC connection to your stratis wallet,
#can be found in stratis.conf
rpc.user=stratisuser
rpc.password=stratispassword

#use the default post 26174
rpc.url=http://127.0.0.1:26174

breeze.ipv4=127.0.0.1
#breeze.ipv6=2001:0db8:85a3:0000:0000:8a2e:0370:7334
#breeze.onion=0123456789ABCDEF
#breeze.port=37123

# for testing purposes, feel free to change these values
#breeze.regtxfeevalue=10000
#breeze.regtxoutputvalue=1000

tumbler.url=http://127.0.0.1:37123/api/v1/

# reference the key file we just generated
#tumbler.rsakeyfile=/home/dan/.breezeserver/Tumbler.pem

# reference the pubkey of the stratisd testnet wallet containing the registration tx fee
# Get a list of your stratisd addresses with `stratisd listaddressgroupings`
#or generate a 'receive' address if you are using stratisX
tumbler.ecdsakeyaddress=<stratisd wallet address>
```

Run the server again with `dotnet run -testnet` within `<path-to-BreezeServer>/Breeze.BreezeServer`, and keep it running.


#### Configuring NTumbleBit to RPC bitcoind

###### Configuration file
After running the server and getting a successful tumbler registration, a configuration directory will be created. You will find a default server.conf file has been generated:

| OS | NTumbleBit/TestNet/ config parent directory |
| --- | --- |
| Linux                   | /home/\<username\>/.ntumblebitserver/          |
| Mac OS                  | /Users/\<username\>/.ntumblebitserver/         |
| Windows Vista, 7, 8, 10 | C:\Users\\<username\>\.ntumblebitserver\       |
| Windows XP              | C:\Documents and Settings\\<username\>\Application Data\.ntumblebitserver\ |

edit the `server.conf` file within the `TestNet` directory to configure RPC with bitcoind and also add your tor settings.
```
# ~/.ntumblebitserver/TestNet/server.conf
rpc.url=http://127.0.0.1:18332/
rpc.user=bitcoinuser
rpc.password=bitcoinpassword

tor.enabled=true
tor.server=127.0.0.1:9051
tor.cookiefile={path to your cookie file}
tor.virtualport=80
```

BreezeServer's configuration file can be found in the user's home directory at `.breezeserver/breeze.conf` or `%appdata%\Breeze.BreezeServer\breeze.conf` on Windows.

| OS | breeze.conf parent directory |
| --- | --- |
| Linux                   | /home/\<username\>/.breezeserver/                                     |
| Mac OS                  | /Users/\<username\>/Library/Application Support/BreezeServer/         |
| Windows Vista, 7, 8, 10 | C:\Users\\<username\>\AppData\Roaming\BreezeServer\                   |
| Windows XP              | C:\Documents and Settings\\<username\>\Application Data\BreezeServer\ |


#### on ubuntu...
```
cp ~/.ntumblebitserver/TestNet/Tumbler.pem ~/.breezeserver/
```

##### Setting up your first stratisd wallet
If you don't yet have a stratis wallet, generate one in stratisd by running the following.
First jump into your stratis user:
```
sudo su - stratis
```
Then generate your wallet
```
stratisd getnewaddress
```
The output of this command is our `tumbler.ecdsakeyaddress` for our conf file.


##### Client

After starting the server, the address of the tumbler will be printed to console. This address can be copied for use in the client.
