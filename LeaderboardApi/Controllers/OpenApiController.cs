using LeaderboardModel;
using LeaderboardService;
using Microsoft.AspNetCore.Mvc;

namespace LeaderboardApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OpenApiController : ControllerBase
    {
        /// <summary>
        /// leaderboard service
        /// </summary>
        private ILeaderboardService _leaderboardService;

        public OpenApiController(ILeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        /// <summary>
        /// init leaderboard data
        /// </summary>
        /// <returns></returns>
        [HttpGet("init")]
        public void InitData()
        {
            foreach (var customerId in Enumerable.Range(1, 20 * 10000).Select(i => (ulong)i))
            {
                _leaderboardService.ModifyCustomerScore(customerId, 1);
            }
        }

        /// <summary>
        /// modify customer score
        /// </summary>
        /// <param name="customerid"></param>
        /// <param name="score"></param>
        /// <returns></returns>
        [HttpPost("customer/{customerid}/score/{score}")]
        public ApiResponse<double> ModifyCustomerScore([FromRoute] ulong customerid, [FromRoute] double score)
        {
            return _leaderboardService.ModifyCustomerScore(customerid, score);
        }

        /// <summary>
        /// get customers by rank
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        [HttpGet("leaderboard")]
        public ApiResponse<CustomerLeaderboardInfoModel[]> GetCustomerByLevelAsync([FromQuery] uint start, [FromQuery] uint end)
        {
            return _leaderboardService.GetCustomerByRank(start, end);
        }

        /// <summary>
        /// get customers by id
        /// </summary>
        /// <param name="customerid"></param>
        /// <param name="high"></param>
        /// <param name="low"></param>
        /// <returns></returns>
        [HttpGet("leaderboard/{customerid}")]
        public ApiResponse<CustomerLeaderboardInfoModel[]> GetCustomerByIdAsync([FromRoute] ulong customerid, [FromQuery] uint high, [FromQuery] uint low)
        {
            return _leaderboardService.GetCustomerById(customerid, high, low);
        }
    }
}
