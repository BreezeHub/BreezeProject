using System;
using System.Collections.Generic;
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
        /// Connects to a masternode running the Breeze Privacy Protocol.
        /// </summary>
        Task<Result<ClassicTumblerParameters>> ConnectToTumblerAsync(HashSet<string> masternodeBlacklist = null);

        /// <summary>
        /// Disconnects from the currently connected masternode and attempts to connect to a new one.
        /// </summary>
        Task<Result<ClassicTumblerParameters>> ChangeServerAsync();

        Task TumbleAsync(string originWalletName, string destinationWalletName, string originWalletPassword);

        /// <summary>
        /// Flip the Breeze Privacy Protocol client runtime to onlymonitor mode.
        /// </summary>
        /// <returns></returns>
        Task OnlyMonitorAsync();

        Task Initialize();

        int RegistrationCount();
        
        /// <summary>
        /// The state of the connection with the masternode server.
        /// </summary>
        TumbleBitManager.TumbleState State { get; }

        string TumblerAddress { get; }

        ClassicTumblerParameters TumblerParameters { get; }

        TumblingState TumblingState { get; }

        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="height">The height of the block in the blockchain.</param>
        /// <param name="block">The block.</param>
        void ProcessBlock(int height, Block block);

        /// <summary>
        /// Last Block time expressed in minutes since the last block.
        /// Returns -1 if the block time is undefined for any reason.
        /// </summary>
        int LastBlockTime { get; }

        /// <summary>
        /// Calculates the tumbling duration, based on the origin wallet and various tumbler parameters.   
        /// Returns the days/hours.
        /// </summary>
        string CalculateTumblingDuration(string originWalletName);
    }
}
