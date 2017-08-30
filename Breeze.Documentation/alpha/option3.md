## How to build the Breeze Daemon

To build breeze wallet with Breeze Privacy Protocol, you need to first build the daemon then the client.

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

# Run a daemon Bitcoin SPV node on testnet
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

## To build for production

- Using development variables (environments/index.ts) :  `npm run electron:dev`
- Using production variables (environments/index.prod.ts) :  `npm run electron:prod`
