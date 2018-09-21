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
    public static class NewsCheckerTimer
    {
        [FunctionName("NewsCheckerTimer")]
        public static void Run([TimerTrigger("0 0 4 * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info("C# CheckNews HTTP trigger function processed a request.");
            NewsChecker.StartNewsCheck(log);
        }
    }

    public static class NewsCheckerTrigger
    {
        [FunctionName("NewsCheckerTrigger")]
        public static void Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("C# CheckNews HTTP trigger function processed a request.");
            NewsChecker.StartNewsCheck(log);
        }
    }

    public class NewsChecker
    {
        public static void StartNewsCheck(TraceWriter log)
        {
            List<News> newsUpdates = Utils.ServiceProxies.GetNews("Technology", log);
            log.Info($"Found {newsUpdates.Count.ToString()} news stories from Bing News, adding to db..");

            foreach (News story in newsUpdates)
            {
                // First check if the company exists, if not add initial tweet data
                if (!CheckIfCompanyExists(story.CompanyName, log))
                {
                    log.Info($"Company {story.CompanyName} not yet in db, adding...");
                    AddCompany(story.CompanyName, story.Symbol, log);
                    // Add tweets
                    //if (story.Symbol != "" && story.Price != "0")
                    //{
                    //    List<Tweet> tweets = ServiceProxies.SearchTweets(story.CompanyName);
                    //    log.Info($"Found {tweets.Count.ToString()} tweets for company {story.CompanyName}, adding...");
                    //    foreach (Tweet tweet in tweets)
                    //    {
                    //        DateTime publishDateTime = DateTime.ParseExact(tweet.DatePublished, "ddd MMM dd HH:mm:ss zzz yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal);
                    //        string Price = ServiceProxies.GetStockPrice(story.Symbol, publishDateTime.ToString("yyyy-MM-dd"), log);
                    //        DataRepository.AddUpdate(story.CompanyName + "_TWEET_" + tweet.DatePublished, story.CompanyName, story.Symbol, "TWEET", tweet.Text, Convert.ToInt32(tweet.FavoriteCount), Convert.ToInt32(tweet.RetweetCount), tweet.Sentiment.ToString(), Price, publishDateTime, log);
                    //    }
                    //}
                }

                // Now add news
                //if (story.Symbol != "" && story.Price != "0")
                //{
                //    // Add news update                                                                                                
                //    DataRepository.AddUpdate(story.CompanyName + "_" + story.PublishDate, story.CompanyName, story.Symbol, "NEWS", story.Title, 0, 0, story.Sentiment, story.Price, System.DateTime.Now, log);
                //    // Add github stars
                //    GitHub gitHubData = ServiceProxies.GetGitHubStars(story.CompanyName);
                //    if (gitHubData.Stars > 0 || gitHubData.Watches > 0)
                //    {
                //        DataRepository.AddUpdate(story.CompanyName + "_GITHUB_" + story.PublishDate, story.CompanyName, story.Symbol, "GITHUB", "", gitHubData.Stars, gitHubData.Watches, "-1", story.Price, System.DateTime.Now, log);
                //    }
                //}
            }
        }
        private static bool CheckIfCompanyExists(string Name, TraceWriter log)
        {
            bool result = false;
            string sqlStatement = $"SELECT COUNT(*) FROM Companies WHERE Name=N'{Name}'";
            if (Name != "")
            {
                try
                {
                    string conString = System.Environment.GetEnvironmentVariable("ConnectionString");
                    using (var connection = new SqlConnection(conString))
                    {
                        var command = new SqlCommand(sqlStatement, connection);
                        connection.Open();
                        int count = (int)command.ExecuteScalar();
                        if (count > 0) result = true;
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"CheckIfCompanyExists error executing SQL Statement {sqlStatement}", ex);
                }
            }

            return result;
        }

        private static void AddCompany(string Name, string Symbol, TraceWriter log)
        {

            if (Name != "")
            {
                string sqlStatement = $"IF NOT EXISTS (SELECT * FROM Companies WHERE Name=N'{Name}') BEGIN INSERT INTO Companies (Name, Symbol, LastUpdate) VALUES ('{Name}', '{Symbol}', '{System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}') END";
                try
                {
                    string conString = System.Environment.GetEnvironmentVariable("ConnectionString");
                    using (var connection = new SqlConnection(conString))
                    {
                        var command = new SqlCommand(sqlStatement, connection);
                        connection.Open();
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    log.Error($"AddCompany error executing SQL Statement {sqlStatement}", ex);
                }
            }
        }
    }
  
}
