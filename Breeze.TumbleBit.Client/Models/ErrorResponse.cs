using System.Collections.Generic;
using Newtonsoft.Json;

namespace Breeze.TumbleBit.Client.Models
{
    public class ErrorResponse
    {
        [JsonProperty(PropertyName = "errors")]
        public List<ErrorModel> Errors { get; set; }
    }
}
