using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NTumbleBit;
using NTumbleBit.BouncyCastle.Math;
using TumbleBitSetup;

namespace Breeze.TumbleBit.Client
{
    /// <summary>
    /// Converter used to convert <see cref="RsaPubKey"/> to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class RsaPubKeyConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(RsaPubKey);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject rsaPubKey = JObject.Load(reader);
            JToken bytesAsString = rsaPubKey["bytes"];

            byte[] bytes = Convert.FromBase64String(bytesAsString.Value<string>());
            return new RsaPubKey(bytes);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var rsaPubKey = value as RsaPubKey;

            writer.WriteStartObject();
            writer.WritePropertyName("bytes");

            byte[] bytes = rsaPubKey.ToBytes();
            writer.WriteValue(Convert.ToBase64String(bytes));
            writer.WriteEndObject();
        }
    }
}
