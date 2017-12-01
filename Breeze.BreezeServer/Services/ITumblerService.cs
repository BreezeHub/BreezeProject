using NTumbleBit.ClassicTumbler.Server;

namespace Breeze.BreezeServer.Services
{
    public interface ITumblerService
    {
        TumblerConfiguration config { get; set; }
        TumblerRuntime runtime { get; set; }
        
        void StartTumbler(BreezeConfiguration breezeConfig, bool getConfigOnly, string ntumblebitServerConf = null, string datadir = null);
    }
}