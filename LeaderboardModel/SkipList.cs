namespace LeaderboardModel
{
    public class SkipList
    {
        // max layer (refer to redis zset)
        private const int _maxLayer = 32;

        // add layer probability (refer to redis zset)
        private const double _addLayerProbability = 0.25;

        // random numbers
        private readonly Random _random = Random.Shared;

        // head
        private readonly NodeInfo _head = new(layer: _maxLayer);

        // init layer
        private int _initLayer = 1;

        public int Count { get; set; }

        /// <summary>
        /// random layer
        /// </summary>
        /// <returns></returns>
        private int RandomLayer()
        {
            int layer = 1;
            while (_random.NextDouble() < _addLayerProbability && layer < _maxLayer)
            {
                layer++;
            }
            return layer;
        }

        /// <summary>
        /// Compare score or customer id
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private int CompareScoreCustomerId(NodeInfo a, NodeInfo b)
        {
            var c = b.score.CompareTo(a.score);
            return c != 0 ? c : a.customerId.CompareTo(b.customerId);
        }

        /// <summary>
        /// Add a node
        /// </summary>
        /// <param name="node"></param>
        public void AddNode(NodeInfo node)
        {
            var nodeArr = new NodeInfo[_maxLayer];
            var rank = new int[_maxLayer];
            var head = _head;

            // Traverse in reverse order to find the insertion node.
            for (int i = _initLayer - 1; i >= 0; i--)
            {
                rank[i] = i == _initLayer - 1 ? 0 : rank[i + 1];
                while (head.nextNode[i] != null && CompareScoreCustomerId(head.nextNode[i], node) < 0)
                {
                    rank[i] += head.intervalSpanCount[i];
                    head = head.nextNode[i];
                }
                nodeArr[i] = head;
            }

            // The height of the newly added node
            int layer = RandomLayer();
            if (layer > _initLayer)
            {
                for (int i = _initLayer; i < layer; i++)
                {
                    nodeArr[i] = _head;
                    nodeArr[i].intervalSpanCount[i] = Count;
                }
                _initLayer = layer;
            }

            // Add and update nodes at each layer
            for (int i = 0; i < layer; i++)
            {
                node.nextNode[i] = nodeArr[i].nextNode[i];
                nodeArr[i].nextNode[i] = node;

                node.intervalSpanCount[i] = nodeArr[i].intervalSpanCount[i] - (rank[0] - rank[i]);
                nodeArr[i].intervalSpanCount[i] = (rank[0] - rank[i]) + 1;
            }

            // modify layer interval span count
            for (int i = layer; i < _initLayer; i++)
            {
                nodeArr[i].intervalSpanCount[i]++;
            }

            Count++;
        }

        /// <summary>
        /// Delete a node
        /// </summary>
        /// <param name="node"></param>
        public void DeleteNode(NodeInfo node)
        {
            var nodeArr = new NodeInfo[_maxLayer];
            var head = _head;

            for (int i = _initLayer - 1; i >= 0; i--)
            {
                while (head.nextNode[i] != null && CompareScoreCustomerId(head.nextNode[i], node) < 0)
                {
                    head = head.nextNode[i];
                }

                nodeArr[i] = head;
            }

            head = head.nextNode[0];
            if (head != node)
            {
                return;
            }

            for (int i = 0; i < _initLayer; i++)
            {
                if (nodeArr[i].nextNode[i] != head)
                {
                    nodeArr[i].intervalSpanCount[i]--;
                }
                else
                {
                    nodeArr[i].intervalSpanCount[i] += head.intervalSpanCount[i] - 1;
                    // Core,delete the node,splicing front and back nodes
                    nodeArr[i].nextNode[i] = head.nextNode[i];
                }
            }

            while (_initLayer > 1 && _head.nextNode[_initLayer - 1] == null)
            {
                _initLayer--;
            }

            Count--;
        }

        /// <summary>
        /// Get current node ranking
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public int GetCurrentNodeRanking(NodeInfo node)
        {
            var rank = 0;
            var head = _head;
            for (int i = _initLayer - 1; i >= 0; i--)
            {
                while (head.nextNode[i] != null && CompareScoreCustomerId(head.nextNode[i], node) < 0)
                {
                    rank += head.intervalSpanCount[i];
                    head = head.nextNode[i];
                }
            }
            return rank + 1;
        }

        /// <summary>
        /// Get range ranking
        /// </summary>
        /// <param name="start"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public IEnumerable<NodeInfo> GetRangeRanking(int start, int count)
        {
            var head = _head;
            int offset = 0;

            for (int i = _initLayer - 1; i >= 0; i--)
            {
                while (head.nextNode[i] != null && offset + head.intervalSpanCount[i] < start)
                {
                    offset += head.intervalSpanCount[i];
                    head = head.nextNode[i];
                }
            }

            head = head.nextNode[0];
            while (head != null && count-- > 0)
            {
                yield return head;
                head = head.nextNode[0];
            }
        }
    }
}
