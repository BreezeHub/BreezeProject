using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BreezeCommon
{
    public class IPAddressConverter : JsonConverter
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="objectType"></param>
		/// <returns></returns>
		public override bool CanConvert(Type objectType)
		{
			if (objectType == typeof(IPAddress)) return true;
			if (objectType == typeof(List<IPAddress>)) return true;

			return false;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // convert an ipaddress represented as a string into an IPAddress object and return it to the caller
            if (objectType == typeof(IPAddress))
            {
                try
                {
                    return IPAddress.Parse(JToken.Load(reader).ToString());
                }
                catch (Exception)
                {
                    return null;
                }
            }

            // convert an array of IPAddresses represented as strings into a List<IPAddress> object and return it to the caller
            if (objectType == typeof(List<IPAddress>))
            {
                try
                {
                    return JToken.Load(reader).Select(address => IPAddress.Parse((string)address)).ToList();
                }
                catch (Exception)
                {
                    return null;
                }
            }

            throw new NotImplementedException();
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="writer"></param>
		/// <param name="value"></param>
		/// <param name="serializer"></param>
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			// convert an IPAddress object to a string representation of itself and Write it to the serialiser
			if (value.GetType() == typeof(IPAddress))
			{
				JToken.FromObject(value.ToString()).WriteTo(writer);
				return;
			}

			// convert a List<IPAddress> object to a string[] representation of itself and Write it to the serialiser
			if (value.GetType() == typeof(List<IPAddress>))
			{
				JToken.FromObject((from n in (List<IPAddress>)value select n.ToString()).ToList()).WriteTo(writer);
				return;
			}

			throw new NotImplementedException();
		}
	}
	
	public class PubKeyConverter : JsonConverter
	{
		/// <inheritdoc />
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(PubKey);
		}

		/// <inheritdoc />
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.Value == null)
				return null;

			return new PubKey(Convert.FromBase64String((string)reader.Value));
		}

		/// <inheritdoc />
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if ((PubKey)value == null)
				writer.WriteNull();
			
			writer.WriteValue(Convert.ToBase64String(((PubKey)value).ToBytes()));
		}
	}
}