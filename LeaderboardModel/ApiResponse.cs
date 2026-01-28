using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LeaderboardModel
{
    public class ApiResponse
    {
        public ApiResponse() { }

        /// <summary>
        /// code
        /// </summary>
        //[JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        /// <summary>
        /// message
        /// </summary>
        //[JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// success response
        /// </summary>
        /// <returns></returns>
        public ApiResponse SetSuccessful(string message = "SUCCESS")
        {
            Success = true;
            Message = message;
            return this;
        }

        /// <summary>
        /// success or failure
        /// </summary>
        /// <returns></returns>
        public bool IsSuccessful()
        {
            return Success;
        }

        /// <summary>
        /// error response
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public ApiResponse SetError(string message)
        {
            Success = false;
            Message = message;
            return this;
        }
    }

    public class ApiResponse<T> : ApiResponse
    {
        /// <summary>
        /// data
        /// </summary>
        //[JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
