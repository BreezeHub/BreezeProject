cd C:\Code\BreezeProject\Breeze\src\Breeze.Daemon
dotnet run --no-build -- -bitcoin -registration -tumblebit -regtest -port=18100 -apiport=19100 -connect=127.0.0.1:18000 -connect=127.0.0.1:28000 -datadir=C:\Code\BreezeProject\UATTestSetup\NodeData\TBClientA -noTor -tumblerProtocol=http
