using NTumbleBit.ClassicTumbler.Server;
using Stratis.Bitcoin.Configuration;

namespace Breeze.BreezeServer.Features.Masternode.Services
{
    public interface ITumblerService
    {
        TumblerConfiguration config { get; set; }
        TumblerRuntime runtime { get; set; }
        
        void StartTumbler(bool getConfigOnly);
    }
}