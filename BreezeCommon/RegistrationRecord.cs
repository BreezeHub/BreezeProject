using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

namespace BreezeCommon
{
    public class RegistrationRecord
    {
		public DateTime RecordTimestamp { get; set; }
        public Guid RecordGuid { get; set; }
        public RegistrationToken Record { get; set; }

        public RegistrationRecord(DateTime recordTimeStamp, Guid recordGuid, RegistrationToken record)
        {
            RecordTimestamp = recordTimeStamp;
            RecordGuid = recordGuid;
            Record = record;
        }
    }
}