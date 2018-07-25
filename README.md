| Windows | Linux | OS X |
| :---- | :------ | :---- |
[![Windows build status][1]][2] | [![Linux build status][3]][4] | [![OS X build status][5]][6] | 

[1]: https://ci.appveyor.com/api/projects/status/kljfu81sunb2cm3l?svg=true
[2]: https://ci.appveyor.com/project/breezehubadmin/breezeproject
[3]: https://travis-ci.org/BreezeHub/BreezeProject.svg?branch=master
[4]: https://travis-ci.org/BreezeHub/BreezeProject
[5]: https://travis-ci.org/BreezeHub/BreezeProject.svg?branch=master
[6]: https://travis-ci.org/BreezeHub/BreezeProject


# Breeze Wallet with NTumbleBit Privacy Protocol

__Warning: This is an experimental build. At the moment, only bitcoin on testnet is supported but more is coming soon. Use at your own risk.__
This is the repository of the Breeze Wallet, the first full-block SPV bitcoin wallet using Angular and Electron at the front-end and C# with .NET Core in the back-end.

This version includes a feature where the Wallet automatically connects to a live Masternode running the Breeze Privacy Protocol.  This is achieved by using the blockchain enabled Masternode Advertisement and Client Discovery Protocol. 

## Daemon Build

Breeze daemon is the backend REST service, hosting a Bitcoin node upon which Breeze UI depends:

```
# Clone and go in the directory
mkdir C:\Opt
cd C:\Opt
git clone https://github.com/breezehub/BreezeProject --recursive

# Go in the Breeze deamon folder
cd C:\Opt\BreezeProject\Breeze\src\Breeze.Daemon

# Run the Bitcoin and Stratis light daemons on testnet in separate terminals
dotnet run -- -testnet -light -registration -tumblebit
dotnet run -- -testnet -light -registration -stratis -addnode=13.64.76.48 -addnode=51.141.51.129 -addnode=98.229.142.125

# Run Breeze GUI
cd C:\Opt\BreezeProject\Breeze.UI

# Install dependencies
npm install

# Start the Breeze GUI using testnet network
npm start -- -testnet
```

## CI Build

We use [AppVeyor](https://www.appveyor.com/) for Windows CI builds and [Travis CI](https://travis-ci.org/) (coming soon) for our Linux and MacOS ones.
Every time someone pushes to the master branch or create a pull request on it, a build is triggered and a new unstable app release is created.

To skip a build, for example if you've made very minor changes, include the text **[skip ci]** or **[ci skip]** in your commits' comment (with the squared brackets).

If you want the :sparkles: latest :sparkles: (unstable :bomb:) version of the Breeze app, you can get it here: 

https://github.com/BreezeHub/BreezeProject/releases

## For **testnet** only.

This is early release, alpha software, is provided for experiment, testing and development purposes and should only be used on **testnet**.

In the case you heed all warnings and choose to run the Mainnet software, be aware that we are still facing an issue where funds get locked in Escrow. It is possible but complicated to manually return funds. Still, beware that you are likely to lose funds.

[7]: https://ci.appveyor.com/api/projects/breezehubadmin/breezeproject/artifacts/breeze_out/breeze-win7-x86-Release.zip?job=Environment%3A%20win_runtime%3Dwin7-x86%2C%20arch%3Dia32%2C%20plat%3Dwin32
[8]: https://ci.appveyor.com/api/projects/breezehubadmin/breezeproject/artifacts/breeze_out/breeze-win7-x64-Release.zip?job=Environment%3A%20win_runtime%3Dwin7-x64%2C%20arch%3Dx64%2C%20plat%3Dwin32
[9]: https://ci.appveyor.com/api/projects/breezehubadmin/breezeproject/artifacts/breeze_out/breeze-win10-x86-Release.zip?job=Environment%3A%20win_runtime%3Dwin10-x86%2C%20arch%3Dia32%2C%20plat%3Dwin32
[10]: https://ci.appveyor.com/api/projects/breezehubadmin/breezeproject/artifacts/breeze_out/breeze-win10-x64-Release.zip?job=Environment%3A%20win_runtime%3Dwin10-x64%2C%20arch%3Dx64%2C%20plat%3Dwin32
[11]: https://github.com/breezehub/BreezeProject/releases/download/cd-unstable/breeze-ubuntu.14.04-x64-Release.zip
[12]: https://github.com/breezehub/BreezeProject/releases/download/cd-unstable/breeze-ubuntu.14.04-x64-Release.zip
[13]: https://github.com/breezehub/BreezeProject/releases/download/cd-unstable/breeze-osx.10.11-x64-Release.zip
[14]: https://github.com/breezehub/BreezeProject/releases/download/cd-unstable/breeze-osx.10.12-x64-Release.zip
