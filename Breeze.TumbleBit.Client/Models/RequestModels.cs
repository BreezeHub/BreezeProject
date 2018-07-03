using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Breeze.TumbleBit.Models
{
    /// <summary>
    /// Base class for request objects received to the controllers
    /// </summary>
    public class RequestModel
    {
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    /// <summary>
    /// Object used to connect to a tumbler.
    /// </summary>
    public class TumblerConnectionRequest : RequestModel
    {
        [Required(ErrorMessage = "A server address is required.")]
        public Uri ServerAddress { get; set; }

        public string Network { get; set; }
    }

    public class TumbleRequest : RequestModel
    {
        [Required(ErrorMessage = "The name of the origin wallet is required.")]
        public string OriginWalletName { get; set; }

        [Required(ErrorMessage = "The name of the destination wallet is required.")]
        public string DestinationWalletName { get; set; }

        [Required(ErrorMessage = "The password of the origin wallet is required.")]
        public string OriginWalletPassword { get; set; }
    }

	public class ConnectRequest : RequestModel
	{
		[Required(ErrorMessage = "The name of the origin wallet is required.")]
		public string OriginWalletName { get; set; }
	}

	public class ChangeServerRequest : RequestModel
	{
		[Required(ErrorMessage = "The name of the origin wallet is required.")]
		public string OriginWalletName { get; set; }
	}

	/// <summary>
	/// Object used to perform a dummy registration.
	/// </summary>
	public class DummyRegistrationRequest : RequestModel
    {
        [Required(ErrorMessage = "A wallet name is required.")]
        public string OriginWallet { get; set; }

        [Required(ErrorMessage = "A wallet password is required.")]
        public string OriginWalletPassword { get; set; }
    }

    /// <summary>
    /// Object used to perform a regtest block generation.
    /// </summary>
    public class BlockGenerateRequest : RequestModel
    {
        [Required(ErrorMessage = "Number of blocks to generate is required")]
        public int NumberOfBlocks { get; set; }
    }
}
