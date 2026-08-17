using System;
using System.Net;

namespace AlbionDataAvalonia.Auth.Services
{
    public class AuthServiceException : Exception
    {
        public HttpStatusCode? StatusCode { get; }
        public bool IsInvalidToken { get; }

        public AuthServiceException(string message, HttpStatusCode? statusCode = null, bool isInvalidToken = false, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            IsInvalidToken = isInvalidToken;
        }

        public static AuthServiceException TokenRejectedError(HttpStatusCode statusCode, string responseBody)
        {
            var isInvalid = statusCode == HttpStatusCode.Unauthorized;
            return new AuthServiceException($"TrimsSilver server rejected the stored token: {statusCode}, {responseBody}", statusCode, isInvalid);
        }

        public static AuthServiceException ProfileFetchError(HttpStatusCode statusCode, string responseBody)
        {
            return new AuthServiceException($"Failed to fetch TrimsSilver profile: {statusCode}, {responseBody}", statusCode);
        }
    }
}
