using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Collections.Generic;
using CogStockFunctions.Dtos;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Data.SqlClient;
using System;
using System.Net;
using CogStockFunctions.Utils;

namespace CogStockFunctions.Functions
{
    //public static class CheckTweetsTimer
    //{
    //    [FunctionName("CheckTweetsTimer")]
    //    public static void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, TraceWriter log)
    //    {
    //        log.Info("C# CheckTweetsTimer timer trigger function processed a request.");
    //        //TweetFunctions.CheckTweets(log);
    //    }
    //}

    public static class CheckTweetsTrigger
    {
        [FunctionName("CheckTweetsTrigger")]
        public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("C# CheckTweetsTrigger HTTP trigger function processed a request.");
            string result = TweetFunctions.CheckTweets(log);
            return new OkObjectResult(result);
        }
    }

    public static class TweetFunctions
    {
        public static string CheckTweets(TraceWriter log)
        {
            string result = "Tweets updated.";

            try
            {
                List<Company> companies = Utils.DataRepository.GetCompaniesFromDb(log);

                foreach (Company company in companies)
                {
                    List<Tweet> tweets = ServiceProxies.SearchTweets(company.Name);
                    log.Info($"Found {tweets.Count.ToString()} tweets for company {company.Name}, adding...");
                    foreach (Tweet tweet in tweets)
                    {
                        DateTime publishDateTime = DateTime.ParseExact(tweet.DatePublished, "ddd MMM dd HH:mm:ss zzz yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal);
                        string Price = ServiceProxies.GetStockPrice(company.Symbol, publishDateTime.ToString("yyyy-MM-dd"), log);
                        DataRepository.AddUpdate(company.Name + "_TWEET_" + tweet.DatePublished, company.Name, company.Symbol, "TWEET", tweet.Text, Convert.ToInt32(tweet.FavoriteCount), Convert.ToInt32(tweet.RetweetCount), tweet.Sentiment.ToString(), Price, publishDateTime, log);
                    }
                }
            }
            catch (Exception ex)
            {

                log.Error(ex.Message, ex);
                result = $"Check tweets error: {ex.ToString()}";
            }

            return result;
        }
    }
}