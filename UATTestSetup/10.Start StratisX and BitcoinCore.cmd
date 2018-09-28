start /b "1" "Stratis-Qt-v2.0.0.5-win.exe" -regtest -port=18001 -server -rpcport=19001 -rpcuser=stratisTestUser -rpcpassword=testPassword -server -datadir=C:\Code\BreezeProject\UATTestSetup\NodeData\StratisCoreA -addnode=127.0.0.1:28001
start /b "1" "Stratis-Qt-v2.0.0.5-win.exe" -regtest -port=28001 -server -rpcport=29001 -rpcuser=stratisTestUser -rpcpassword=testPassword -server -datadir=C:\Code\BreezeProject\UATTestSetup\NodeData\StratisCoreB

start /b "1" "bitcoin-0.15.1-win64.exe" -regtest -port=18000 -server -rpcport=19000 -rpcuser=bitcoinTestUser -rpcpassword=testPassword -datadir=C:\Code\BreezeProject\UATTestSetup\NodeData\BitcoinCoreA -addnode=127.0.0.1:28000
start /b "1" "bitcoin-0.15.1-win64.exe" -regtest -port=28000 -server -rpcport=29000 -rpcuser=bitcoinTestUser -rpcpassword=testPassword -datadir=C:\Code\BreezeProject\UATTestSetup\NodeData\BitcoinCoreB
