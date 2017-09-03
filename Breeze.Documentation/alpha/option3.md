## How to build Breeze Wallet

To build breeze wallet with Breeze Privacy Protocol, you need to first build and run Breeze Daemon.

Breeze daemon is the backend REST service, hosting a Bitcoin node upon which Breeze UI depends:

```
# Clone and go in the directory
git clone https://github.com/breezehub/Breeze
cd Breeze

# Initialize dependencies
git submodule update --init --recursive

# Go in Breeze's solution folder
cd Breeze
dotnet restore
dotnet build

# Run a daemon Bitcoin SPV node on testnet with tumblebit
cd src/Breeze.Daemon
dotnet run light -testnet -tumblebit
```

To build the client:

Navigate to the Breeze UI in a terminal:
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

