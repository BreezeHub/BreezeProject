using NBitcoin;
using System;
using Microsoft.Extensions.Logging;
using NTumbleBit.Logging;

namespace NTumbleBit
{
    public abstract class EscrowReceiver : IEscrow
    {
        public class State
        {
            public ScriptCoin EscrowedCoin { get; set; }
            public Key EscrowKey { get; set; }

            /// <summary>
            /// Identify the channel to the tumbler
            /// </summary>
            public uint160 ChannelId { get; set; }
        }

        protected State InternalState { get; set; }

        public uint160 Id
        {
            get { return InternalState.ChannelId; }
        }

        public void SetChannelId(uint160 channelId)
        {
            InternalState.ChannelId = channelId ?? throw new ArgumentNullException(nameof(channelId));

            Logs.Tumbler.LogDebug($"ChannelId : {InternalState.ChannelId}");
        }

        public virtual void ConfigureEscrowedCoin(ScriptCoin escrowedCoin, Key escrowKey)
        {
            InternalState.EscrowKey = escrowKey ?? throw new ArgumentNullException(nameof(escrowKey));
            InternalState.EscrowedCoin = escrowedCoin ?? throw new ArgumentNullException(nameof(escrowedCoin));

            Logs.Tumbler.LogDebug(
                $"EscrowedCoin.Outpoint.Hash : {InternalState.EscrowedCoin.Outpoint.Hash}, EscrowedCoin.Outpoint.N : {InternalState.EscrowedCoin.Outpoint.N}");
        }

        public ScriptCoin EscrowedCoin
        {
            get { return InternalState.EscrowedCoin; }
        }
    }
}