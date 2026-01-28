using System.Text.Json.Serialization;

namespace LeaderboardModel
{
    /// <summary>
    /// struct：value types;contiguius memory;stack operations;smaller size;faster speed;no gc pressure
    /// </summary>
    public struct CustomerLeaderboardInfoModel
    {
        /// <summary>
        /// customer id
        /// </summary>
        //[JsonPropertyName("customer_id")]
        public ulong CustomerId { get; set; }

        /// <summary>
        /// score
        /// </summary>
        //[JsonPropertyName("score")]
        public double Score { get; set; }

        /// <summary>
        /// ranking
        /// </summary>
        //[JsonPropertyName("ranking")]
        public int Ranking { get; set; }

        public CustomerLeaderboardInfoModel(ulong customerId, double score,int ranking)
        {
            CustomerId = customerId;
            Score = score;
            Ranking = ranking;
        }
    }
}
