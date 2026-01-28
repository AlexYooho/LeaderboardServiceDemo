namespace LeaderboardModel
{
    public struct ModifyScoreModel
    {
        /// <summary>
        /// customer id
        /// </summary>
        public ulong CustomerId { get; init; }

        /// <summary>
        /// score
        /// </summary>
        public long Score { get; init; }

        /// <summary>
        /// current score
        /// </summary>
        public TaskCompletionSource<ApiResponse<double>> CurrentScore { get; init; }

    }
}
