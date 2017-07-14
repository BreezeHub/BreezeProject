using System;

using NBitcoin;
using NBitcoin.RPC;

namespace Breeze.BreezeD
{
    public class TransactionUtils
    {
        public Transaction FundRawTx(RPCClient rpc, Transaction rawTx, Money feeAmount, BitcoinAddress changeAddress)
        {
            // The transaction funding logic will ensure that a transaction fee of
            // feeAmount is included. The remaining difference between the value of
            // the inputs and the outputs will be returned as a change address
            // output

            var unspentOutputs = rpc.ListUnspent();
            var totalFunded = new Money(0);

            foreach (var unspent in unspentOutputs)
            {
                if (!unspent.IsSpendable)
                    continue;

                if (totalFunded < (rawTx.TotalOut + feeAmount))
                {
                    rawTx.Inputs.Add(new TxIn()
                    {
                        PrevOut = unspent.OutPoint
                    });

                    // By this point the input array will have at least one element
                    // starting at index 0
                    rawTx.Inputs[rawTx.Inputs.Count - 1].ScriptSig = unspent.ScriptPubKey;
                    
                    // Need to accurately account for how much funding is assigned
                    // to the inputs so that change can be correctly calculated later
                    totalFunded += unspent.Amount;
                }
                else
                {
                    break;
                }
            }

            if (totalFunded < (rawTx.TotalOut + feeAmount))
                throw new Exception("Insufficient unspent funds for registration");

            var change = totalFunded - rawTx.TotalOut - feeAmount;

            if (change < 0)
                throw new Exception("Change amount cannot be negative for registration transaction");

            rawTx.Outputs.Add(new TxOut()
            {
                Value = change,
                ScriptPubKey = changeAddress.ScriptPubKey
            });

            return rawTx;
        }
    }
}