# Running Breeze Wallet with Breeze Privacy Protocol (alpha)

Use these steps to setup Breeze Wallet that includes the Privacy Protocol.

### Prerequisites
Breeze wallet is dependent on [node](https://nodejs.org/) and [dotnetcore](https://www.microsoft.com/net/core).  Please install these frameworks on your system before proceeding.

The Privacy Protocol requires Tor. In the Windows version Tor is configured automatically.  If you already have tor running you should instead use [option three](https://github.com/BreezeHub/Breeze/blob/tumblebit-alpha/Breeze.Documentation/alpha/option3.md) and setup Tor yourself.

* [Node download](https://nodejs.org/en/download/)
* [.Net Core download](https://www.microsoft.com/net/core)

Next download the alpha version of breeze wallet and unzip it to a location of your choosing.

[release matrix here]

Issue the following command inside the Breeze Alpha folder:

```run```

This process will start up Tor, start a background window and the Breeze Wallet.  The background window communicates progress information. This info will be integrated into the Privacy user interface in the near future.

Mac OSX

Mac users need to install Tor prior to running the alpha.  It can be installed via Homebrew.

``` 
//usr//bin//ruby -e "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install)
brew install tor 
```

Navigate to your tor folder and issue this command to start Tor:

```
tor -controlport 9051 -cookieauthentication 1
```

Enjoy and please give us [feedback](https://stratisplatform.slack.com/messages/C5F5GGLC8/), [contribute](https://github.com/BreezeHub) and join us on the [slack](https://stratisplatform.slack.com/messages/C5F5GGLC8/).

