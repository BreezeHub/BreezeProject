# BreezeServer

BreezeServer is an implementation of [TumbleBit](http://tumblebit.cash) in .NET Core. It is an untrusted bitcoin-compatible anonymous payments protocol.

## The BreezeServer Experimental Release

This release includes the following:

- **Node Advertisment Protocol**

  A high level overview of the protocol *operations* performed by each Breeze TumbleBit Server is as follows:

  1. The node operator starts up the BreezeServer software
  2. The node checks to see if it has registered itself on the Stratis blockchain before
  3. If it has, the tumbler service is initialized as normal
  4. If the node has not yet registered, or if its configuration has changed The registration transaction updates and is broadcast again.

- **Registration Transaction**

  The registration transaction is a specially-formatted transaction broadcast by the Breeze TumbleBit Server to the Stratis network. In this release, the registration transactions are broadcast to the main Stratis blockchain.

- **Security Features**

  The registration transaction carries the following information:

  1. The IP address of the Breeze TumbleBit Server
  2. (Currently optional) TOR address of the server
  3. The port that wallets should use to connect.
  4. All the information is signed by the tumbler’s private keys. This means that the signatures can be validated by a Breeze wallet when it connects to the Breeze Tumblebit Server. The registration protocol will greatly benefit from widespread testing by the Stratis community.

As this is alpha software, the tumbler is currently configured to only operate on the Bitcoin testnet. This is to prevent loss of funds in the event of errors. Once the tumbler is sufficiently stable, a Bitcoin mainnet version will be released.


