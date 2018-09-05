using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.RPC;
using NTumbleBit;
using NTumbleBit.Logging;
using NTumbleBit.Services;
using Utils = NTumbleBit.Utils;

namespace Breeze.BreezeServer.Features.Masternode.Services.FullNodeBatches
{
	public class ReceiveBatch : BatchBase<ClientEscapeData, Transaction>
	{
	    private IWalletService walletService;

        public ReceiveBatch(IWalletService walletService)
        {
            this.walletService = walletService;
        }

		public FeeRate FeeRate
		{
			get; set;
		}
		
		protected override async Task<Transaction> RunAsync(ClientEscapeData[] data)
		{
		    var cashout = await walletService.GenerateAddressAsync();
		    var tx = new Transaction();

		    foreach (var input in data)
		    {
		        // Note placeholders - this step is performed again further on
		        var txin = new TxIn(input.EscrowedCoin.Outpoint);
		        txin.ScriptSig = new Script(
		            Op.GetPushOp(TrustedBroadcastRequest.PlaceholderSignature),
		            Op.GetPushOp(TrustedBroadcastRequest.PlaceholderSignature),
		            Op.GetPushOp(input.EscrowedCoin.Redeem.ToBytes())
		        );
		        txin.Witnessify();
		        tx.AddInput(txin);
		    }
            
		    tx.Outputs.Add(new TxOut()
		    {
		        ScriptPubKey = cashout.ScriptPubKey,
		        Value = data.Select(c => c.EscrowedCoin.Amount).Sum()
            });

		    var currentFee = tx.GetFee(data.Select(d => d.EscrowedCoin).ToArray());
		    tx.Outputs[0].Value -= FeeRate.GetFee(tx) - currentFee;

		    for (int i = 0; i < data.Length; i++)
		    {
		        var input = data[i];
                var txin = tx.Inputs[i];
		        var signature = tx.SignInput(input.EscrowKey, input.EscrowedCoin);
		        txin.ScriptSig = new Script(
		            Op.GetPushOp(input.ClientSignature.ToBytes()),
		            Op.GetPushOp(signature.ToBytes()),
		            Op.GetPushOp(input.EscrowedCoin.Redeem.ToBytes())
		        );
		        txin.Witnessify();
            }

            //Do not broadcast here, MainController takes care of it
            return tx;
		}
	}

}
