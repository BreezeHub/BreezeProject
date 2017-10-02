using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using BreezeCommon;

namespace Stratis.MasterNode.Features.InterNodeComms
{
	//This service discovery bahavior currently supports only 'tumblebit',  However
	//it would be easy to use the ServiceDiscoveryPayload.ServiceName property to
	//implement routing back to a dictionary of stores to make this fully generic.
    public class ServiceDiscoveryBehavior : NodeBehavior
    {
        private Timer BroadcastTimer = null;
	    private IReadOnlyList<DiscoveryCapsule> capsules;
        private RegistrationStore store;

		public ServiceDiscoveryBehavior(IReadOnlyList<DiscoveryCapsule> capsules, RegistrationStore store)
		{
			this.capsules = capsules;
            this.store = store;
		}

        public override object Clone()
        {
            return new ServiceDiscoveryBehavior(new List<DiscoveryCapsule>(this.capsules), this.store);
        }

        protected override void AttachCore()
        {
            this.BroadcastTimer = new Timer(o =>
            {
                this.Broadcast();

            }, null, 0, (int)TimeSpan.FromMinutes(1).TotalMilliseconds);

            //tell someone to clean up after us
            this.RegisterDisposable(this.BroadcastTimer);

            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
        }

        private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            if (message.Message.Payload is ServiceDiscoveryPayload)
            {
	            var serviceDiscovery = (ServiceDiscoveryPayload) message.Message.Payload;
	            if (serviceDiscovery.ServiceName != "tumblebit") return;

	            //get list
				var incomingList = new List<DiscoveryCapsule>(serviceDiscovery.Capsules);

	            //if we are synced ...
	            if (AreEquivalent(this.capsules, incomingList))
	            {
                    //...merge back to the store here

                    Dictionary<string, bool> recordIds = new Dictionary<string, bool>();

                    foreach (RegistrationRecord record in this.store.GetAll())
                    {
                        recordIds[record.RecordTxId] = true;
                    }

                    // Check if any in synced list are missing from the store and add them
                    foreach (RegistrationCapsule capsule in this.capsules)
                    {
                        if (!recordIds.ContainsKey(capsule.RegistrationTransaction.GetHash().ToString()))
                        {
                            this.store.AddCapsule(capsule, node.Network);
                        }
                    }

		            return;
	            }

				//we are differnet - so update
	            this.capsules = Merge(this.capsules, incomingList);
            }
        }

        void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
			if (node.State == NodeState.Connected)
				this.Broadcast();
        }

        void Broadcast()
        {
            if (this.AttachedNode != null)
            {
                if (this.AttachedNode.State == NodeState.HandShaked)
                {
                    this.AttachedNode.SendMessageAsync(new ServiceDiscoveryPayload("tumblebit", new List<DiscoveryCapsule>(this.capsules).ToArray()));
                }
            }
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceived;
        }

	    internal static bool AreEquivalent(IReadOnlyList<DiscoveryCapsule> list1, IReadOnlyList<DiscoveryCapsule> list2)
	    {
		    var listOne = list1.ToImmutableSortedSet();
		    var listTwo = list2.ToImmutableSortedSet();
		    return listOne.SequenceEqual(listTwo);
	    }

	    internal static IReadOnlyList<DiscoveryCapsule> Merge(IReadOnlyList<DiscoveryCapsule> list1, IReadOnlyList<DiscoveryCapsule> list2)
	    {
		    return list1.Union(list2).ToArray();
	    }
    }
}
