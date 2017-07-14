using System;
using System.Net;

using NBitcoin;
using NBitcoin.RPC;

namespace Breeze.BreezeD
{
    public class RPCHelper
    {
        Network RPCNetwork;

        public RPCHelper(Network network)
        {
            RPCNetwork = network;
        }

        public RPCClient GetClient(string rpcUser, string rpcPassword, string rpcUrl)
        {
            NetworkCredential credentials = new NetworkCredential(rpcUser, rpcPassword);
            RPCClient rpc = new RPCClient(credentials, new Uri(rpcUrl), RPCNetwork);

            return rpc;    
        }
    }
}
