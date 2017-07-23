using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;

namespace BreezeCommon
{
    public class RegistrationRecord
    {
		public DateTime RecordTimestamp { get; set; }
        public RegistrationToken Record { get; set; }

        public RegistrationRecord(DateTime recordTimeStamp, RegistrationToken record)
        {
            RecordTimestamp = recordTimeStamp;
            Record = record;
        }
    }
}