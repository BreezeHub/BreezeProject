# Remove previous alpha data

If you ran the previous alpha version of Breeze, your wallet files will be incompatible with the new ones. <br>
This requires you to remove those wallet files before starting the new wallet binary. If you had any funds in the previous wallet, you will be able to restore it with your mnemonic and password. <br>
We describe the process in detail below. 

## Windows
- Use the address bar in Windows Explorer to navigate to %AppData%/StratisNode.
- Remove all of its contents.

## OS X
- Open a terminal window
- Run the command:
```
rm -rf ~/.stratisnode
```

## Ubuntu
- Open a terminal window
- Run the command:
```
rm -rf ~/.stratisnode
```