using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TumbleBitSetup;

namespace Breeze.TumbleBit.Client
{
    /// <summary>
    /// Converter used to convert <see cref="PermutationTestProof"/> to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class PermutationTestProofConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PermutationTestProof);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject permutationProofTest = JObject.Load(reader);
            JToken signatures = permutationProofTest["signatures"];
            List<byte[]> signatureList = new List<byte[]>();

            foreach (var signature in signatures)
            {
                signatureList.Add(Convert.FromBase64String(signature.Value<string>()));
            }

            return new PermutationTestProof(signatureList.ToArray());
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("signatures");
            writer.WriteStartArray();
            foreach (var signature in ((PermutationTestProof)value).Signatures)
            {
                writer.WriteValue(Convert.ToBase64String(signature));
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}
