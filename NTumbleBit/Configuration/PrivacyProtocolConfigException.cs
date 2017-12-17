namespace NTumbleBit.Configuration
{
    public enum PrivacyProtocolType
    {
        Tor = 1
    }

	public class PrivacyProtocolConfigException : ConfigException
	{
        public PrivacyProtocolType PrivacyProtocolType { get; private set; }

        public PrivacyProtocolConfigException(PrivacyProtocolType type)
        {
            this.PrivacyProtocolType = type;
        }

	    public PrivacyProtocolConfigException(PrivacyProtocolType type, string message) : base(message)
	    {
	        this.PrivacyProtocolType = type;
        }

	    public PrivacyProtocolConfigException(PrivacyProtocolType type, ConfigException ex) : base(ex.Message)
	    {
	        this.PrivacyProtocolType = type;
        }
    }
}
