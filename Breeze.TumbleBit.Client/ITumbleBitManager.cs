using System;
using System.Threading.Tasks;
using Breeze.TumbleBit.Client.Models;
using NBitcoin;
using NTumbleBit.ClassicTumbler;

namespace Breeze.TumbleBit.Client
{
    /// <summary>
    /// An interface for managing interactions with the TumbleBit service.
    /// </summary>
    public interface ITumbleBitManager : IDisposable
    {
        /// <summary>
        /// Connects to the tumbler.
        /// </summary>
        /// <returns></returns>
        Task<ClassicTumblerParameters> ConnectToTumblerAsync();

        Task TumbleAsync(string originWalletName, string destinationWalletName, string originWalletPassword);

        /// <summary>
        /// Flip the tumbler to onlymonitor mode.
        /// </summary>
        /// <returns></returns>
        Task OnlyMonitorAsync();

        Task Initialize();

        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="height">The height of the block in the blockchain.</param>
        /// <param name="block">The block.</param>
        void ProcessBlock(int height, Block block);

        Task DummyRegistration(string originWalletName, string originWalletPassword);

        /// <summary>
        /// The state of the tumbler
        /// </summary>
        TumbleBitManager.TumbleState State { get; }

        string TumblerAddress { get; }

        ClassicTumblerParameters TumblerParameters { get; }

        TumblingState tumblingState { get; }
    }
}
