# Changing the Breeze Server that your Breeze Wallet uses for Privacy

The uri address used to connect the Breeze Wallet to a Breeze Server is not editable from the user interface in this release.

```
We do not recommend you change your server if you have already started tumbling coins.
```

If you wish to change it you can start the Daemon with the command line, for example:

```
dotnet run light -testnet - tumblebit -ppuri=ctb://7obtcd7mkosmxeuh.onion/?h=03c632023c4a8587845ad918b8e5f53f7bf18319
```

You can see [here](https://github.com/BreezeHub/Breeze/blob/tumblebit-alpha/Breeze.Documentation/alpha/option3.md) how to do this in more detail.

Enjoy and please give us [feedback](https://stratisplatform.slack.com/messages/C5F5GGLC8/), [contribute](https://github.com/BreezeHub) and join us on the [slack](https://stratisplatform.slack.com/messages/C5F5GGLC8/).

