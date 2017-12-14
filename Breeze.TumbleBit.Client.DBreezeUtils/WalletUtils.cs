using System.IO;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;

namespace Breeze.TumbleBit.Client.DBreezeUtils
{
    public class WalletUtils
    {
        private Wallet _sourceWallet;
        private Wallet _destWallet;
        private CoinType _coinType;
        
        public WalletUtils(string walletPath, string sourceWalletFileName, string destWalletFileName, CoinType coinType = CoinType.Bitcoin)
        {
            _sourceWallet = JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(Path.Combine(walletPath, sourceWalletFileName)));
            _destWallet = JsonConvert.DeserializeObject<Wallet>(File.ReadAllText(Path.Combine(walletPath, destWalletFileName)));

            _coinType = coinType;
        }

        public bool FindTransaction(string txId)
        {
            foreach (var tx in _sourceWallet.GetAllTransactionsByCoinType(_coinType))
            {
                if (tx.Id.ToString().Equals(txId))
                    return true;
            }

            foreach (var tx in _destWallet.GetAllTransactionsByCoinType(_coinType))
            {
                if (tx.Id.ToString().Equals(txId))
                    return true;
            }

            return false;
        }

        /*
        public Money GetFee(string txId)
        {
            foreach (var tx in _sourceWallet.GetAllTransactionsByCoinType(_coinType))
            {
                if (tx.Id.ToString().Equals(txId))
                    tx.SpendingDetails.
            }

            foreach (var tx in _destWallet.GetAllTransactionsByCoinType(_coinType))
            {
                if (tx.Id.ToString().Equals(txId))
                    return true;
            }            
        }
        */
    }
}
