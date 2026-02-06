using LeaderboardModel;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace LeaderboardService
{
    /// <summary>
    /// V4 based on SkipList
    /// </summary>
    public class LeaderboardServiceV4Impl : ILeaderboardService
    {
        // score precision
        private const int _scorePrecision = 100;

        // query range
        private const int _searchByRankQueryRange = 100;
        private const int _searchByIdQueryRange = 50;

        // customer node info dictionay
        private readonly ConcurrentDictionary<ulong, NodeInfo> _customerNodeInfoDic = new();

        // skiplist
        private readonly SkipList _skipList = new();

        // read writer lock
        private readonly ReaderWriterLockSlim _lock = new();

        // cache
        private readonly IMemoryCache _cache;

        private const string _rangeRankCacheKeyPrefix = "range:rank:";
        private const string _userRangeRankCacheKeyPrefix = "user:range:rank:";

        // cache expiration time
        private readonly TimeSpan _cacheExpirationTime = TimeSpan.FromSeconds(5);

        private readonly ConcurrentDictionary<ulong, ConcurrentBag<string>> _userCacheKeyDic = new();

        private readonly ConcurrentDictionary<ulong, ConcurrentBag<string>> _rangeRankCacheKeyDic = new();

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="cache"></param>
        public LeaderboardServiceV4Impl(IMemoryCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// modify customer score
        /// </summary>
        /// <param name="customerId"></param>
        /// <param name="score"></param>
        /// <returns></returns>
        public ApiResponse<double> ModifyCustomerScore(ulong customerId, double score)
        {
            var result = new ApiResponse<double>();
            var modifyScore = (long)(score * _scorePrecision);

            _lock.EnterWriteLock();
            try
            {
                if (_customerNodeInfoDic.TryGetValue(customerId, out var node))
                {
                    _skipList.DeleteNode(node);
                    node.score = Math.Max(0, node.score + modifyScore);
                    if (node.score > 0)
                    {
                        _skipList.AddNode(node);
                    }
                    else
                    {
                        _customerNodeInfoDic.TryRemove(customerId, out _);
                    }
                }
                else if (modifyScore > 0)
                {
                    var newNode = new NodeInfo(customerId, modifyScore);
                    _customerNodeInfoDic[customerId] = newNode;
                    _skipList.AddNode(newNode);
                }

                // clear cache
                ClearCache(customerId);

                result.SetSuccessful();
                result.Data = _customerNodeInfoDic.TryGetValue(customerId, out var n) ? n.score / (double)_scorePrecision : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Modify exception]:{ex.Message}");
                result.SetError("The system is busy,please try again later.");
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            return result;
        }

        /// <summary>
        /// Search customer by rank
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public ApiResponse<CustomerLeaderboardInfoModel[]> GetCustomerByRank(uint start, uint end)
        {
            var result = new ApiResponse<CustomerLeaderboardInfoModel[]>();
            if (start >= end)
            {
                result.SetError("Parameter error,end must be greater than start");
                return result;
            }

            var queryRangeCnt = end - start;
            if (queryRangeCnt > _searchByRankQueryRange)
            {
                result.SetError($"The query range needs to be within {_searchByRankQueryRange},currently:{queryRangeCnt}");
                return result;
            }

            // hotsopt ranking query
            var cacheKey = $"{_rangeRankCacheKeyPrefix}{start}:{queryRangeCnt}";
            if (_cache.TryGetValue(cacheKey, out CustomerLeaderboardInfoModel[] cachedList))
            {
                result.Data = cachedList;
                result.SetSuccessful();
                return result;
            }

            _lock.EnterReadLock();
            try
            {
                int rank = (int)start;
                var list = _skipList.GetRangeRanking((int)start, (int)queryRangeCnt)
                    .Select(n => new CustomerLeaderboardInfoModel(
                        n.customerId,
                        n.score / (double)_scorePrecision,
                        rank++))
                    .ToArray();

                _cache.Set(cacheKey, list, _cacheExpirationTime);
                _rangeRankCacheKeyDic.AddOrUpdate(0UL, _ => new ConcurrentBag<string> { cacheKey }, (_, x) => { x.Add(cacheKey); return x; });

                result.Data = list;
                result.SetSuccessful();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Query exception]:{ex.Message}");
                result.SetError("The system is busy,please try again later.");
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return result;
        }

        /// <summary>
        /// Search customer by id
        /// </summary>
        /// <param name="customerId"></param>
        /// <param name="high"></param>
        /// <param name="low"></param>
        /// <returns></returns>
        public ApiResponse<CustomerLeaderboardInfoModel[]> GetCustomerById(ulong customerId, uint high, uint low)
        {
            var result = new ApiResponse<CustomerLeaderboardInfoModel[]>();
            if (!_customerNodeInfoDic.TryGetValue(customerId, out var node))
            {
                result.SetSuccessful("No ranking");
                return result;
            }

            if (high > _searchByIdQueryRange || low > _searchByIdQueryRange)
            {
                result.SetError($"The parameter high or low must be less than {_searchByIdQueryRange}.");
                return result;
            }

            // hotsopt ranking query
            var cacheKey = $"{_userRangeRankCacheKeyPrefix}{customerId}:{high}:{low}";
            if (_cache.TryGetValue(cacheKey, out CustomerLeaderboardInfoModel[] cachedList))
            {
                result.Data = cachedList;
                result.SetSuccessful();
                return result;
            }

            _lock.EnterReadLock();
            try
            {
                var currentCustomerRank = _skipList.GetCurrentNodeRanking(node);
                var start = Math.Max(1, currentCustomerRank - (int)high);
                var end = currentCustomerRank + (int)low;

                int rank = (int)start;
                var list = _skipList.GetRangeRanking(start, end - start + 1)
                    .Select(n => new CustomerLeaderboardInfoModel(
                        n.customerId,
                        n.score / (double)_scorePrecision,
                        rank++))
                    .ToArray();

                _cache.Set(cacheKey, list, _cacheExpirationTime);
                _userCacheKeyDic.AddOrUpdate(customerId, _ => new ConcurrentBag<string> { cacheKey }, (_, x) => { x.Add(cacheKey); return x; });

                result.Data = list;
                result.SetSuccessful();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Query exception]:{ex.Message}");
                result.SetError("The system is busy,please try again later.");
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return result;
        }

        /// <summary>
        /// clear cache
        /// </summary>
        /// <param name="customerId"></param>
        private void ClearCache(ulong customerId)
        {
            if (_rangeRankCacheKeyDic.TryRemove(0UL, out var rangeKeys))
            {
                foreach (var key in rangeKeys.ToArray())
                {
                    _cache.Remove(key);
                }
            }

            if (_userCacheKeyDic.TryRemove(customerId, out var userKeys))
            {
                foreach (var key in userKeys.ToArray())
                {
                    _cache.Remove(key);
                }
            }
        }
    }
}
