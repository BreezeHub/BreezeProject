namespace Breeze.BreezeD
{
    public class TumblerParameters
    {
        public string Network { get; set; }
        public CycleGenerator cycleGenerator { get; set; }
        public string ServerKey { get; set; }
        public string VoucherKey { get; set; }
        public long Denomination { get; set; }
        public long Fee { get; set; }
        public long FakePuzzleCount { get; set; }
        public long RealPuzzleCount { get; set; }
        public long FakeTransactionCount { get; set; }
        public long RealTransactionCount { get; set; }
        public string FakeFormat { get; set; }
    }
    
    public class FirstCycle
    {
        public int start { get; set; }
        public int registrationDuration { get; set; }
        public int clientChannelEstablishmentDuration { get; set; }
        public int tumblerChannelEstablishmentDuration { get; set; }
        public int tumblerCashoutDuration { get; set; }
        public int clientCashoutDuration { get; set; }
        public int paymentPhaseDuration { get; set; }
        public int safetyPeriodDuration { get; set; }
    }

    public class CycleGenerator
    {
        public int registrationOverlap { get; set; }
        public FirstCycle firstCycle { get; set; }
    }
}