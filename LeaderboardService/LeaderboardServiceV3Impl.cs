using LeaderboardModel;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace LeaderboardService
{
    public class LeaderboardServiceV3Impl : ILeaderboardService, IDisposable
    {
        // score precision,round to two decimal places
        private const int _scorePrecision = 100;

        // query range
        private const int _searchByRankQueryRange = 100;
        private const int _searchByIdQueryRange = 50;

        // customerId -> score
        private readonly ConcurrentDictionary<ulong, long> _customerScoreDic = new();

        // leaderboard list data source
        private volatile CustomerLeaderboardInfoModel[] _leaderboardDataSources = Array.Empty<CustomerLeaderboardInfoModel>();

        // with score customerId -> rank
        private volatile Dictionary<ulong, int> _customerRankDic = new();

        // without score  customerId -> _
        private volatile ConcurrentDictionary<ulong, bool> _customerWithoutRankDic = new();

        // cts
        private readonly CancellationTokenSource _cts = new();

        // build task
        private readonly Task _refresTask;

        // need refresh（0/1）
        private int _needRefresh;

        public LeaderboardServiceV3Impl()
        {
            _refresTask = Task.Factory.StartNew(RefreshLeaderboardProcess, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        }

        #region Write

        /// <summary>
        /// modify customer score
        /// </summary>
        /// <param name="customerId"></param>
        /// <param name="score"></param>
        /// <returns></returns>
        public ApiResponse<double> ModifyCustomerScore(ulong customerId, double score)
        {
            var result = new ApiResponse<double>();
            try
            {
                if (score < -1000 || score > 1000)
                {
                    result.SetError("Error score.");
                    return result;
                }

                var targetScore = (long)Math.Round(score * _scorePrecision);
                var newScore = _customerScoreDic.AddOrUpdate(customerId, targetScore <= 0 ? 0 : targetScore, (_, old) => old + targetScore <= 0 ? 0 : old + targetScore);
                if (newScore <= 0)
                {
                    _customerScoreDic.TryRemove(customerId, out _);
                    _customerWithoutRankDic.TryAdd(customerId, true);
                }

                Interlocked.Exchange(ref _needRefresh, 1);

                result.Data = newScore / (double)_scorePrecision;
                result.SetSuccessful();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Modify exception]:{ex.Message}");

                result.SetError("The system is busy,please try again later.");
                return result;
            }
        }
        #endregion

        #region Query

        /// <summary>
        /// Search customer by rank
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public ApiResponse<CustomerLeaderboardInfoModel[]> GetCustomerByRank(uint start, uint end)
        {
            var result = new ApiResponse<CustomerLeaderboardInfoModel[]>();
            try
            {
                var leaderboards = _leaderboardDataSources;
                if (leaderboards.Length == 0)
                {
                    result.SetSuccessful("No ranking.");
                    return result;
                }

                start = Math.Max(1, start) - 1;
                end = Math.Min(end, (uint)leaderboards.Length);
                if (start >= end)
                {
                    result.SetError("The parameter start must be less than end");
                    return result;
                }

                if (end - start > _searchByRankQueryRange)
                {
                    result.SetError($"The query range needs to be within {_searchByRankQueryRange},currently:{end - start}");
                    return result;
                }

                var count = end - start;
                var arr = new CustomerLeaderboardInfoModel[count];
                Array.Copy(leaderboards, start, arr, 0, count);

                result.Data = arr;
                result.SetSuccessful();

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Query exception]:{ex.Message}");

                result.SetError("The system is busy,please try again later.");
                return result;
            }
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
            try
            {
                if (high > _searchByIdQueryRange || low > _searchByIdQueryRange)
                {
                    result.SetError($"The parameter high or low must be less than {_searchByIdQueryRange}.");
                    return result;
                }

                var leaderboards = _leaderboardDataSources;

                if (_customerWithoutRankDic.ContainsKey(customerId))
                {
                    var arrLength = leaderboards.Length <= high ? leaderboards.Length + 1 : (int)high + 1;
                    var targetArr = new CustomerLeaderboardInfoModel[arrLength];
                    var arrStartIndex = Math.Max(0, leaderboards.Length - high);
                    Array.Copy(leaderboards, arrStartIndex, targetArr, 0, arrLength - 1);

                    // add yourself
                    targetArr[targetArr.Length - 1] = new CustomerLeaderboardInfoModel(customerId, 0, leaderboards.Length + 1);

                    result.Data = targetArr;
                    result.SetSuccessful();
                    return result;
                }

                if (!_customerRankDic.TryGetValue(customerId, out var rank))
                {
                    result.SetSuccessful("No ranking.");
                    return result;
                }

                var startIndex = Math.Max(0, rank - 1 - (int)high);
                var endIndex = Math.Min(leaderboards.Length - 1, rank + (int)low);

                var length = endIndex - startIndex;
                var arr = new CustomerLeaderboardInfoModel[length];
                Array.Copy(leaderboards, startIndex, arr, 0, length);

                result.Data = arr;
                result.SetSuccessful();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Query exception]:{ex.Message}");

                result.SetError("The system is busy,please try again later.");
                return result;
            }
        }

        #endregion

        #region Refresh leaderboard core

        private int _index = 0;

        /// <summary>
        /// Refresh leaderboard
        /// </summary>
        /// <returns></returns>
        private async Task RefreshLeaderboardProcess()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                if (Interlocked.Exchange(ref _needRefresh, 0) == 0)
                {
                    await Task.Delay(5, _cts.Token);
                    continue;
                }

                Interlocked.Increment(ref _index);

                var sw = Stopwatch.StartNew();

                BuildLeaderboardProcess();

                sw.Stop();
                Console.WriteLine($"Refresh leaderboard count：{_index},Refresh leaderboard duration：{sw.Elapsed.TotalMilliseconds}ms");
            }
        }

        /// <summary>
        /// Build leaderboard processor
        /// </summary>
        private void BuildLeaderboardProcess()
        {
            var list = new List<CustomerLeaderboardInfoModel>(_customerScoreDic.Count);
            foreach (var item in _customerScoreDic)
            {
                list.Add(new CustomerLeaderboardInfoModel(item.Key, item.Value / (double)_scorePrecision, 0));
            }

            list.Sort((a, b) =>
            {
                var c = b.Score.CompareTo(a.Score);
                return c != 0 ? c : a.CustomerId.CompareTo(b.CustomerId);
            });

            var customerRankDic = new Dictionary<ulong, int>(list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                item.Ranking = i + 1;
                list[i] = item;
                customerRankDic[item.CustomerId] = item.Ranking;
            }

            _customerRankDic = customerRankDic;
            _leaderboardDataSources = list.ToArray();
        }

        #endregion

        public void Dispose()
        {
            _cts.Cancel();
            Task.WaitAll(_refresTask);
            _cts.Dispose();
        }

    }
}
