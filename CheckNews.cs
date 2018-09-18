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
    public static class CheckNews
    {
        // [FunctionName("CheckNews")]
        // public static void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, TraceWriter log)
        // {
        //     log.Info("C# CheckNews HTTP trigger function processed a request.");
        //     StartNewsCheck(log);
        // }

        [FunctionName("CheckNews")]
        public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            StartNewsCheck(log);
            return new OkObjectResult($"Checked news.");
        }

        private static void StartNewsCheck(TraceWriter log) {
            List<News> newsUpdates = Utils.ServiceProxies.GetNews("Technology", log);
            log.Info($"Found {newsUpdates.Count.ToString()} news stories from Bing News, adding to db..");

            foreach (News story in newsUpdates) {
                // First check if the company exists, if not add initial tweet data
                if (!CheckIfCompanyExists(story.CompanyName, log)) {
                    log.Info($"Company {story.CompanyName} not yet in db, adding...");
                    AddCompany(story.CompanyName, story.Symbol, log);
                    // Add tweets
                    if (story.Symbol != "" && story.Price != "0") {
                        List<Tweet> tweets = ServiceProxies.SearchTweets(story.CompanyName);
                        log.Info($"Found {tweets.Count.ToString()} tweets for company {story.CompanyName}, adding...");
                        foreach (Tweet tweet in tweets) {
                            DateTime publishDateTime = DateTime.ParseExact(tweet.DatePublished, "ddd MMM dd HH:mm:ss zzz yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal);
                            string Price = ServiceProxies.GetStockPrice(story.Symbol, publishDateTime.ToString("yyyy-MM-dd"), log);
                            DataRepository.AddUpdate(story.CompanyName + "_TWEET_" + tweet.DatePublished, story.CompanyName, story.Symbol, "TWEET", tweet.Text, Convert.ToInt32(tweet.FavoriteCount), Convert.ToInt32(tweet.RetweetCount), tweet.Sentiment, Price, publishDateTime, log);
                        }
                    }
                }

                // Now add news
                if (story.Symbol != "" && story.Price != "0") {
                    // Add news update                                                                                                
                    DataRepository.AddUpdate(story.CompanyName + "_" + story.PublishDate, story.CompanyName, story.Symbol, "NEWS", story.Title, 0, 0, story.Sentiment, story.Price, System.DateTime.Now, log);
                    // Add github stars
                    GitHub gitHubData = ServiceProxies.GetGitHubStars(story.CompanyName);
                    if (gitHubData.Stars > 0 || gitHubData.Watches > 0) {
                        DataRepository.AddUpdate(story.CompanyName + "_GITHUB_" + story.PublishDate, story.CompanyName, story.Symbol, "GITHUB", "", gitHubData.Stars, gitHubData.Watches, "-1", story.Price, System.DateTime.Now, log);
                    }
                }                
            }
        }
        private static bool CheckIfCompanyExists(string Name, TraceWriter log) {
            bool result = false;
            string sqlStatement = $"SELECT COUNT(*) FROM Companies WHERE Name=N'{Name}'";
            if (Name != "") {
                try {
                    string conString = System.Environment.GetEnvironmentVariable("ConnectionString");
                    using (var connection = new SqlConnection(conString))
                    {
                        var command = new SqlCommand(sqlStatement, connection);
                        connection.Open();
                        int count = (int)command.ExecuteScalar();
                        if (count > 0) result = true;
                    }    
                }
                catch (Exception ex) {
                    log.Error($"CheckIfCompanyExists error executing SQL Statement {sqlStatement}", ex);
                }
            }

            return result;      
        }

        private static void AddCompany(string Name, string Symbol, TraceWriter log) {

            if (Name != "") {
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
                catch (Exception ex) {
                    log.Error($"AddCompany error executing SQL Statement {sqlStatement}", ex);
                }
            }
        }

        // private static void UpdateTweets() {
        //     AuthenticationResponse auth = GetTwitterAuthenticationToken();

        //     using (var connection = new SqlConnection("Server=tcp:clashserver.database.windows.net,1433;Initial Catalog=clashofaisql;Persist Security Info=False;User ID=clashuser;Password=Passwort123!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"))
        //     {
        //         var command = new SqlCommand($"SELECT * FROM Companies WHERE Symbol <> N''", connection);
        //         connection.Open();
        //         SqlDataReader reader = command.ExecuteReader();
        //         while (reader.Read())
        //         {
        //             string Name = (string) reader["Name"];
        //             string Symbol = (string) reader["Symbol"];
        //             string Price = GetStockPrice(Symbol);
        //             if (Price != "0") {
        //                 List<Tweet> tweets = SearchTweets(Name, auth);
        //                 foreach (Tweet tweet in tweets) {
        //                     AddUpdate(Name + "_TWEET_" + tweet.DatePublished, Name, Symbol, "TWEET", tweet.Text, Convert.ToInt32(tweet.FavoriteCount), Convert.ToInt32(tweet.RetweetCount), tweet.Sentiment, Price);
        //                 }
        //             }
        //         }

        //         // Call Close when done reading.
        //         reader.Close();
        //     }    
        // }

           
    }
  
}
