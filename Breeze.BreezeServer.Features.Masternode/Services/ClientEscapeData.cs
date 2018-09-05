using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Breeze.BreezeServer.Features.Masternode.Services
{
    public class ClientEscapeData
    {
        public ScriptCoin EscrowedCoin { get; set; }
        public TransactionSignature ClientSignature { get; set; }
        public Key EscrowKey { get; set; }
    }
}
