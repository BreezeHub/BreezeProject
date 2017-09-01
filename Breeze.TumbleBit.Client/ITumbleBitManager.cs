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
    public interface ITumbleBitManager
    {
        /// <summary>
        /// Connects to the tumbler.
        /// </summary>
        /// <param name="serverAddress">The URI of the tumbler.</param>
        /// <returns></returns>
        Task<ClassicTumblerParameters> ConnectToTumblerAsync();

        Task TumbleAsync(string originWalletName, string destinationWalletName, string originWalletPassword);

        /// <summary>
        /// Returns whether the tumbler is tumbling.
        /// </summary>
        /// <returns></returns>
        Task<bool> IsTumblingAsync();

        /// <summary>
        /// Stops the tumbler if it is tumbling.
        /// </summary>
        /// <returns></returns>
        Task StopAsync();

        /// <summary>
        /// Processes a block received from the network.
        /// </summary>
        /// <param name="height">The height of the block in the blockchain.</param>
        /// <param name="block">The block.</param>
        void ProcessBlock(int height, Block block);

        /// <summary>
        /// Pauses the tumbling.
        /// </summary>
        void PauseTumbling();

        /// <summary>
        /// Finishes the tumbling and clean up all saved data.
        /// </summary>
        void FinishTumbling();

        /// <summary>
        /// tumbler address
        /// </summary>
        string TumblerAddress { get; }

        /// <summary>
        /// Method for interrogating whether server has already been connected to
        /// </summary>
        bool IsConnected();

        /// <summary>
        /// Method for interrogating whether tumbling client has been initialised & is running
        /// </summary>
        bool IsTumbling();

        /// <summary>
        /// Method to retrieve tumbler parameters if connection has been previously established
        /// </summary>
        /// <returns></returns>
        ClassicTumblerParameters GetTumblerParameters();
    }
}
