using NTumbleBit.ClassicTumbler.Server;

namespace Breeze.BreezeServer.Features.Masternode.Services
{
    public interface ITumblerService
    {
        TumblerConfiguration config { get; set; }
        TumblerRuntime runtime { get; set; }
        
        void StartTumbler(BreezeConfiguration breezeConfig, bool getConfigOnly, string ntumblebitServerConf = null, string datadir = null, bool torMandatory = true, TumblerProtocolType? tumblerProtocol = null);
    }
}