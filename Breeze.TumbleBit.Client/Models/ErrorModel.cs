using System;
using System.Net;
using Newtonsoft.Json;

namespace Breeze.TumbleBit.Client.Models
{
    public class ErrorModel : Stratis.Bitcoin.Utilities.JsonErrors.ErrorModel
    {
        public ErrorModel() { }

        private ErrorModel(
            HttpStatusCode code,
            string message,
            string description,
            Exception ex = null,
            string additionalInfoUrl = null)
        {
            this.Status = (int)code;
            if (ex != null) this.DeveloperMessage = ex.Message;
            this.Message = message;
            this.Description = description;
            if (!string.IsNullOrEmpty(additionalInfoUrl)) this.AdditionalInfoUrl = additionalInfoUrl;
        }

        public static ErrorModel Create(
            HttpStatusCode code,
            string message,
            string description,
            Exception ex = null,
            string additionalInfoUrl = null)
        {
            return new ErrorModel(code, message, description, ex, additionalInfoUrl);
        }

        [JsonProperty(PropertyName = "additionalInfoUrl")]
        public string AdditionalInfoUrl { get; set; }

        [JsonProperty(PropertyName = "developerMessage")]
        public string DeveloperMessage { get; set; }
    }
}