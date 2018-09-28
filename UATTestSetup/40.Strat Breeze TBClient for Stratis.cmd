cd C:\Code\BreezeProject\Breeze\src\Breeze.Daemon
dotnet run --no-build -- -stratis -registration -tumblebit -regtest -port=18101 -apiport=19101 -connect=127.0.0.1:18001 -connect=127.0.0.1:28001 -datadir=C:\Code\BreezeProject\UATTestSetup\NodeData\TBClientA -noTor -tumblerProtocol=http
