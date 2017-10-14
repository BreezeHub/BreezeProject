using System.Threading.Tasks;
using NTumbleBit.ClassicTumbler;

namespace Breeze.TumbleBit.Client
{
    /// <summary>
    /// The tumbler service communicating with the tumbler server.
    /// </summary>
    public interface ITumblerService
    {
        /// <summary>
        /// Gets the tumbler's parameters.
        /// </summary>
        /// <returns></returns>
        Task<ClassicTumblerParameters> GetClassicTumblerParametersAsync();
    }
}
