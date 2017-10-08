using System;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin.Protocol
{
	public class DiscoveryCapsule : IBitcoinSerializable
	{
		public DiscoveryCapsule()
		{
			
		}

		public virtual void ReadWrite(BitcoinStream stream)
		{
			//override
		}
	}

	[Payload("discovery")]
	public class ServiceDiscoveryPayload : Payload
	{
		private readonly string serviceName;

		private DiscoveryCapsule[] capsules;

		public ServiceDiscoveryPayload(string serviceName, DiscoveryCapsule[] capsules)
		{
			this.serviceName = serviceName;
			this.capsules = capsules;
		}

		public DiscoveryCapsule[] Capsules {
			get { return this.capsules; }
			set { this.capsules = value; }
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite<DiscoveryCapsule>(ref capsules);
		}

		public string ServiceName
		{
			get { return this.serviceName; }
		}

		public override string ToString()
		{
			return $"Service: {this.serviceName}";
		}
	}
}
