namespace LeaderboardModel
{
    public class NodeInfo
    {
        /// <summary>
        /// customer id
        /// </summary>
        public ulong customerId;

        /// <summary>
        /// customer score
        /// </summary>
        public long score;

        /// <summary>
        /// next node
        /// </summary>
        public NodeInfo[] nextNode;

        /// <summary>
        /// interval span count
        /// </summary>
        public int[] intervalSpanCount;

        public NodeInfo(int layer, ulong id = 0, long score = 0)
        {
            customerId = id;
            this.score = score;
            nextNode = new NodeInfo[layer];
            intervalSpanCount = new int[layer];
        }
    }
}
