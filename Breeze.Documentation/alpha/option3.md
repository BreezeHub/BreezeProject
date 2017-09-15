## Compiling and running Breeze Wallet
If you prefer to compile and run everything yourself, please follow these steps closely.

## Install and run TOR
To ensure greater privacy while running the Breeze Wallet we enforce running tor.<br/>
Please have a look at our [guide](./tor.md) and follow its instructions for your operating system.<br/>
Note that you will have to run TOR each time you want to start the wallet.

## Compile and run the Daemon
How to build and run Breeze Wallet

To build breeze wallet with Breeze Privacy Protocol, you need to first build and run Breeze Daemon.

Breeze daemon is the backend REST service, hosting a Bitcoin node upon which Breeze UI depends:

```
# Clone and go in the directory
git clone https://github.com/breezehub/Breeze
cd Breeze

#change to required branch
git checkout tumblebit-alpha

# Initialize dependencies
git submodule update --init --recursive

# Go in Breeze's solution folder
cd Breeze
dotnet restore
dotnet build

# Run a daemon Bitcoin SPV node on testnet with tumblebit
cd src/Breeze.Daemon
dotnet run light -testnet -tumblebit -ppuri=ctb://t4cqwqlvswcyyagg.onion?h=f2c96151dc6a76a906d683a20e40e1b8ac01e8b6
```

To build the client:

Open a new terminal and navigate to the Breeze UI folder:
``` bash
cd ./Breeze/Breeze.UI
```

## Install dependencies with npm:

From within Breeze.UI directory run:

``` bash
npm install
```

To run the app in debug mode:

```
npm start
```

## To build for production

- Using development variables (environments/index.ts) :  `npm run electron:dev`
- Using production variables (environments/index.prod.ts) :  `npm run electron:prod`

Enjoy and please give us [feedback](https://stratisplatform.slack.com/messages/C5F5GGLC8/), [contribute](https://github.com/BreezeHub) and join us on the [slack](https://stratisplatform.slack.com/messages/C5F5GGLC8/).

