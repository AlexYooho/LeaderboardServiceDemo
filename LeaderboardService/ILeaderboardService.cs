using LeaderboardModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace LeaderboardService
{
    public interface ILeaderboardService
    {
        /// <summary>
        /// modify customer score
        /// </summary>
        /// <param name="customerId"></param>
        /// <param name="score"></param>
        /// <returns></returns>
        ApiResponse<double> ModifyCustomerScore(ulong customerId, double score);

        /// <summary>
        /// get customers by rank
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        ApiResponse<CustomerLeaderboardInfoModel[]> GetCustomerByRank(uint start, uint end);

        /// <summary>
        /// get customers by id
        /// </summary>
        /// <param name="customerId"></param>
        /// <param name="high"></param>
        /// <param name="low"></param>
        /// <returns></returns>
        ApiResponse<CustomerLeaderboardInfoModel[]> GetCustomerById(ulong customerId, uint high, uint low);
    }
}
