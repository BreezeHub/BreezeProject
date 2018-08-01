using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace BreezeCommon
{
    public static class RegistrationParameters
    {
        public static readonly Money MASTERNODE_COLLATERAL_THRESHOLD = new Money(250000, MoneyUnit.BTC);
        public static readonly int MAX_PROTOCOL_VERSION = 128; // >128 = regard as test versions
        public static readonly int MIN_PROTOCOL_VERSION = 1;
        public static readonly int WINDOW_PERIOD_BLOCK_COUNT = 30;
        public static readonly int REGISTRATION_MATURITY_BLOCK_COUNT = 10;
    }
}
