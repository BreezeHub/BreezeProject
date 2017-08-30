using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Breeze.TumbleBit.Client.Models
{
    public class WatchOnlyBalances
    {
        public Money Confirmed { get; set; }

        public Money Unconfirmed { get; set; }
    }
}
