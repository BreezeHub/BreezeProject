using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;
using NBitcoin;

namespace BreezeCommon
{
    public class RegistrationRecord
    {
        public DateTime RecordTimestamp { get; set; }
        public Guid RecordGuid { get; set; }
        public string RecordTxId { get; set; }
        public string RecordTxHex { get; set; }
        public RegistrationToken Record { get; set; }
        public int BlockReceived { get; set; }
        public bool RegistrationMature { get; set; }

        //[JsonProperty("recordTxProof", NullValueHandling = NullValueHandling.Ignore)]
        [JsonIgnore]
        public PartialMerkleTree RecordTxProof { get; set; }

        public RegistrationRecord(DateTime recordTimeStamp, Guid recordGuid, string recordTxId, string recordTxHex, RegistrationToken record, PartialMerkleTree recordTxProof, int blockReceived = -1)
        {
            RecordTimestamp = recordTimeStamp;
            RecordGuid = recordGuid;
            RecordTxId = recordTxId;
            RecordTxHex = recordTxHex;
            Record = record;
            RecordTxProof = recordTxProof;
            BlockReceived = blockReceived;
            RegistrationMature = false;
        }
    }
}