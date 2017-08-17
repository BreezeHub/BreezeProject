using System.Threading.Tasks;
using Breeze.TumbleBit.Models;
using NBitcoin;
using NTumbleBit;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using PuzzlePromise = NTumbleBit.PuzzlePromise;
using PuzzleSolver = NTumbleBit.PuzzleSolver;

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
