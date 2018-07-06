using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NBitcoin;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.Services;
using NTumbleBit.Services.RPC;

namespace Breeze.TumbleBit.Client.DBreezeUtils
{
    public class Tx
    {
        public string TxId { get; set; }
        public Transaction TxData { get; set; }

        public Tx()
        {
        }

        public Tx(string txId, Transaction txData)
        {
            TxId = txId;
            TxData = txData;
        }

        public override string ToString()
        {
            return TxId;
        }
    }
    
    public class DBreezeUtils : IDisposable
    {
        private string _path;
        private DBreezeRepository _repository;
        private Network _network;

        public DBreezeUtils(string path, Network network)
        {
            _path = path;
            _repository = new DBreezeRepository(path);
            _network = network;
        }

	    public void Dispose()
	    {
		    _repository.Dispose();
		}

        public List<string> GetServerAddresses()
        {
            var servers = new List<string>();
            var tracker = new Tracker(_repository, _network);

            // Get list of tumbler parameters
            foreach (var elem in _repository.List<ClassicTumblerParameters>("Configuration"))
            {
                servers.Add(elem.ExpectedAddress);
            }

            //Repository.Get<ClassicTumblerParameters>("Configuration", configuration.TumblerServer.ToString());

            return servers;
        }

        public List<int> GetCycles()
        {
            var cycles = new List<int>();

            // Enumerate all cycles by listing db2 directory and matching Cycle_nnn... folder
            foreach (var dir1 in Directory.EnumerateDirectories(_path))
            {
                var dir = Path.GetRelativePath(_path, dir1);

                // Not sure what cycle 0 represents or where it comes from
                if (dir.StartsWith("Cycle_") && !dir.Equals("Cycle_0"))
                {
                    cycles.Add(int.Parse(dir.Substring(6)));
                }
            }

            return cycles;
        }

        public TrackerRecord[] GetCycleRecords(int cycle)
        {
            /* Exists in Tracker already, left here for convenience*/
            var tracker = new Tracker(_repository, _network);

            return tracker.GetRecords(cycle);
            
        }

        public List<string> FindAllTransactions(TransactionType type)
        {
            var txns = new List<string>();
            var tracker = new Tracker(_repository, _network);
            
            foreach (var cycle in GetCycles())
            {
                foreach (var record in tracker.GetRecords(cycle))
                {
                    if (record.TransactionType == type)
                    {
                        if (record.TransactionId != null)
                        {
                            txns.Add(record.TransactionId.ToString());
                        }
                        else
                        {
                            // These might be null if a particular cycle was interrupted
                        }
                    }
                }
            }
    
            return txns;
        }

        public List<Tx> FindAllBroadcastTransactions()
        {
            var transactions = new List<Tx>();
            var tracker = new Tracker(_repository, Network.Main);
            
            // Get list of Broadcast service transactions
            foreach (var elem in _repository.List<RPCBroadcastService.Record>("Broadcasts"))
            {
                transactions.Add(new Tx()
                {
                    TxId = elem.Transaction.GetHash().ToString(),
                    TxData = elem.Transaction
                });
            }
            
            return transactions;
        }

        public List<Tx> FindAllTrustedBroadcastTransactions()
        {
            var transactions = new List<Tx>();
            var tracker = new Tracker(_repository, Network.Main);
            
            // Get list of Broadcast service transactions
            foreach (var elem in _repository.List<RPCTrustedBroadcastService.Record>("TrustedBroadcasts"))
            {
                transactions.Add(new Tx()
                {
                    TxId = elem.Request.Transaction.GetHash().ToString(),
                    TxData = elem.Request.Transaction
                });
            }
            
            return transactions;            
        }

        public List<Tx> FindAllTxToRecordEntries()
        {
            var transactions = new List<Tx>();
            var tracker = new Tracker(_repository, Network.Main);
            
            // Get list of Broadcast service transactions
            foreach (var elem in _repository.List<RPCTrustedBroadcastService.TxToRecord>("TxToRecord"))
            {
                transactions.Add(new Tx()
                {
                    TxId = elem.RecordHash.ToString(),
                    TxData = elem.Transaction
                });
            }
            
            return transactions;
        }
    }
}
