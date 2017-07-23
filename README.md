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

(*** **Update 23rd July 2017:** *We are continuing to work on this documentation please check back for regular updates.* ***)

BreezeServer is a service host for our implementation of [TumbleBit](http://tumblebit.cash) in .NET Core. It is an untrusted<sup>*</sup> bitcoin-compatible anonymous payments protocol.

<sup>*</sup>Here *untrusted*, is used in a cryptographic context to indicate that the TumbleBit protocol does not require trust to perform its function.

## The BreezeServer Experimental Release
This release includes the following:

###### Node Advertisment Protocol

  A high level overview of the protocol *operations* performed by each Breeze TumbleBit Server is as follows:
  1. The node operator starts up the BreezeServer software
  2. The node checks to see if it has registered itself on the Stratis blockchain before
  3. If it has registered, the tumbler service is initialized as normal
  4. If the node has not yet registered, or if its configuration has changed, the registration transaction updates and is broadcast again.
  5. Once registered the service is ready for connections from BreezeClients.

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
  - [.NET Core SDK 1.0.4](https://github.com/dotnet/core/blob/master/release-notes/download-archives/1.0.4-sdk-download.md) (see below)
  - [Bitcoin Core 0.13.1](https://bitcoin.org/bin/bitcoin-core-0.13.1/) fully synched, rpc enabled

You can easily install the SDK on ubuntu systems after installing the runtime by running:
```
sudo apt-get install dotnet-dev-1.0.4
```
More information about installing .NET Core on your system can be found [here](https://www.microsoft.com/net/core). Later versions of Bitcoin Core should work as well.

If you are a developer or want to browse the code, you additionally require:
  - [Visual Studio Code](https://code.visualstudio.com/) with [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp) (cross platform)
  - [Visual Studio 2017](https://www.visualstudio.com/downloads/) (Windows only)

#### Configuring Bitcoin Core for Testnet

[Download](https://bitcoin.org/bin/bitcoin-core-0.13.1/) Bitcoin Core 0.13.1

Create/edit your bitcoin.conf:

```
# bitcoin.conf

# Run on the test network instead of the real bitcoin network.
testnet=1

# server=1 tells Bitcoin-Qt and bitcoind to accept JSON-RPC commands
server=1

# RPC user and password. We don't yet support cookie authentication
rpcuser=bitcoinuser
rpcpassword=bitcoinuser

# Optional: pruning can reduce the disk usage from currently around 120GB (or 8GB testnet) to around 2 GB.
prune=2000
```
Place the file in the relevant directory based on your system:

| OS | bitcoin.conf parent directory |
| --- | --- |
| Linux                   | /home/<username>/.bitcoin/                                     |
| Mac OS                  | /Users/<username>/Library/Application Support/Bitcoin/         |
| Windows Vista, 7, 8, 10 | C:\Users\<username>\AppData\Roaming\Bitcoin\                   |
| Windows XP              | C:\Documents and Settings\<username>\Application Data\Bitcoin\ |

Finally, boot up bitcoind or bitcoin-qt, let it sync with the network, and send it some coins.

#### Installing .NET Core

NTumbleBit is built upon .NET Core.

You will need to install it on your system following [these instructions](https://www.microsoft.com/net/core).

#### Getting the Code

Navigate to where you would like to save the code in your shell and then:
```
git clone git@github.com:BreezeHub/BreezeServer.git --recursive
```
The `--recursive` command is vital because BreezeServer relies on git submodules

#### `dotnet restore`

According to the documentation, > The `dotnet restore` command uses NuGet to restore dependencies as well as project-specific tools that are specified in the project file. By default, the restoration of dependencies and tools are performed in parallel.+

run `dotnet restore` inside the newly created BreezeServer directory

#### Running Tests

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

#### Configure & Run

###### Notes before you start
- Your wallet is only as secure as your configuration files, so apply appropriate permissions.
- We are working on the testnet from here on.

###### Server
After installing .NET Core, launching bitcoind on testnet, and restoring dependencies via `dotnet restore`

First run the server and the configuration will be generated for you.

```
cd Breeze.BreezeServer
dotnet run -testnet
```
The server's configuration file can be found in the user's home directory at `.breezeserver/breeze.conf` or `%appdata%\Breeze.BreezeServer\breeze.conf` on Windows.

```
# server.conf
# Sample Configuration file:

rpc.url=http://localhost:18332/  # assumes Bitcoin Core is on localhost  
rpc.user=bitcoinuser         # use the credentials from your bitcoin.conf
rpc.password=bitcoinuser     # use the credentials from your bitcoin.conf
```

Run the server again with `dotnet run -testnet`, and keep it running.

###### Client

After starting the server, the address of the tumbler will be printed to console. The client config key `tumbler.server` should be set to this address.

At this stage the client is hosted  in [NTumbleBit](https://github.com/ntumblebit/ntumblebit)

Breeze.BreezeClient is not implemented yet. Please view the [documentation](https://github.com/ntumblebit/ntumblebit/wiki/How-to-Run#the-client) for NTumbleBit Client

###### The registration transaction

###### FAQ
Please read the [FAQ](https://github.com/BreezeHub/BreezeServer/wiki/FAQ) if you are struggling.
