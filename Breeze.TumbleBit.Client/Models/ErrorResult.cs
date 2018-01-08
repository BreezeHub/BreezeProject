using Microsoft.AspNetCore.Mvc;

namespace Breeze.TumbleBit.Client.Models
{
    public class ErrorResult : ObjectResult
    {
        public ErrorResult(int statusCode, ErrorResponse value) : base((object) value)
        {
            this.StatusCode = statusCode;
        }
    }
}