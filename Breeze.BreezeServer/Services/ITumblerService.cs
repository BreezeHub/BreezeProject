using NTumbleBit.ClassicTumbler.Server;

namespace Breeze.BreezeServer
{
    public interface ITumblerService
    {
        TumblerConfiguration config { get; set; }
        TumblerRuntime runtime { get; set; }
        
        void StartTumbler(bool testnet, bool getConfigOnly);
    }
}