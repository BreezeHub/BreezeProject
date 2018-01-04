using System;
using System.Collections.Generic;
using System.Net;
using Breeze.TumbleBit.Client.Models;

namespace Breeze.TumbleBit.Client
{
    public static class ErrorHelpers
    {
        public static ErrorResult BuildErrorResponse(
            HttpStatusCode statusCode,
            string message,
            string description,
            Exception ex = null,
            string additionalInfoUrl = null)
        {
            var errorResponse = new ErrorResponse
            {
                Errors = new List<ErrorModel>
                {
                    ErrorModel.Create(statusCode, message, description, ex, additionalInfoUrl)
                }
            };

            return new ErrorResult((int)statusCode, errorResponse);
        }
    }
}
