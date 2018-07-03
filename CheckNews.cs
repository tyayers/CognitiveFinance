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
        [FunctionName("CheckNews")]
        public static void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            List<News> newsUpdates = Utils.ServiceProxies.GetNews();
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
                            AddUpdate(story.CompanyName + "_TWEET_" + story.PublishDate, story.CompanyName, story.Symbol, "TWEET", tweet.Text, Convert.ToInt32(tweet.FavoriteCount), Convert.ToInt32(tweet.RetweetCount), tweet.Sentiment, story.Price, log);
                        }
                    }
                }

                // Now add news
                if (story.Symbol != "" && story.Price != "0") {
                    // Add news update                                                                                                
                    AddUpdate(story.CompanyName + "_" + story.PublishDate, story.CompanyName, story.Symbol, "NEWS", story.Title, 0, 0, story.Sentiment, story.Price, log);
                    // Add github stars
                    int gitHubStars = ServiceProxies.GetGitHubStars(story.CompanyName);
                    if (gitHubStars > 0) {
                        AddUpdate(story.CompanyName + "_GHSTARS_" + story.PublishDate, story.CompanyName, story.Symbol, "GHSTARS", "", gitHubStars, 0, "-1", story.Price, log);
                    }
                }                
            }
        }

        // private static List<News> GetNews()
        // {
        //     List<News> results = new List<News>();

        //     using (HttpClient client = new HttpClient())
        //     {
        //         client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "170b9892d8cc491e90401b645cc0b8af");
        //         string rssContent = client.GetStringAsync("https://api.cognitive.microsoft.com/bing/v7.0/news?category=technology&mkt=en-us").Result;

        //         Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(rssContent);

        //         if (obj != null) {
        //             AuthenticationResponse auth = GetTwitterAuthenticationToken();
            
        //             foreach (JObject newsItem in obj["value"]) {
        //                 string title = newsItem["name"].ToString();
        //                 string description = newsItem["description"].ToString();
        //                 string datePublished = newsItem["datePublished"].ToString();
        //                 string sentiment = GetSentitment(title + " " + description);
        //                 List<Company> companies = GetCompanies(title);

        //                 if (companies != null) {
        //                     foreach (Company comp in companies) {
        //                         string Price = GetStockPrice(comp.Symbol);

        //                         if (!CheckIfCompanyExists(comp.Name)) {
        //                             AddCompany(comp.Name, comp.Symbol);
        //                             // Add tweets
        //                             if (comp.Symbol != "" && Price != "0") {
        //                                 List<Tweet> tweets = SearchTweets(comp.Name, auth);
        //                                 foreach (Tweet tweet in tweets) {
        //                                     AddUpdate(comp.Name + "_TWEET_" + datePublished, comp.Name, comp.Symbol, "TWEET", tweet.Text, Convert.ToInt32(tweet.FavoriteCount), Convert.ToInt32(tweet.RetweetCount), tweet.Sentiment, Price);
        //                                 }
        //                             }
        //                         }
        //                         if (comp.Symbol != "" && Price != "0") {
        //                             // Add news update                                                                                                
        //                             AddUpdate(comp.Name + "_" + datePublished, comp.Name, comp.Symbol, "NEWS", title, 0, 0, sentiment, Price);
        //                             // Add github stars
        //                             int gitHubStars = GetGitHubStars(comp.Name);
        //                             if (gitHubStars > 0) {
        //                                 AddUpdate(comp.Name + "_GHSTARS_" + datePublished, comp.Name, comp.Symbol, "GHSTARS", "", gitHubStars, 0, "-1", Price);
        //                             }
        //                         }
        //                     }
        //                 }
        //             }
        //         }

        //         return results;
        //     }
        // }













        private static bool CheckIfCompanyExists(string Name, TraceWriter log) {
            bool result = false;
            string sqlStatement = $"SELECT COUNT(*) FROM Companies WHERE Name=N'{Name}'";
            if (Name != "") {
                try {
                    using (var connection = new SqlConnection("Server=tcp:clashserver.database.windows.net,1433;Initial Catalog=clashofaisql;Persist Security Info=False;User ID=clashuser;Password=Passwort123!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"))
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
                    using (var connection = new SqlConnection("Server=tcp:clashserver.database.windows.net,1433;Initial Catalog=clashofaisql;Persist Security Info=False;User ID=clashuser;Password=Passwort123!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"))
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

        private static void AddUpdate(string Id, string Name, string Symbol, string Type, string Text, int Metric, int Metric2, string Sentiment, string StockPrice, TraceWriter log) {
            string sqlStatement = $"IF NOT EXISTS (SELECT * FROM Updates WHERE Id=N'{Id}') BEGIN INSERT INTO Updates (Id, Name, Symbol, Type, Text, Metric, Metric2, Sentiment, StockPrice, LastUpdate) VALUES (N'{Id}', N'{Name}', N'{Symbol}', '{Type}', N'{Text.Replace("'", "").Replace("\"", "")}', {Metric}, {Metric2}, {Sentiment}, {StockPrice}, '{System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}') END";

            try {
                using (var connection = new SqlConnection("Server=tcp:clashserver.database.windows.net,1433;Initial Catalog=clashofaisql;Persist Security Info=False;User ID=clashuser;Password=Passwort123!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"))
                {
                    var command = new SqlCommand(sqlStatement, connection);
                    connection.Open();
                    command.ExecuteNonQuery();
                }  
            }
            catch (Exception ex) {
                log.Error($"AddUpdate error executing SQL Statement {sqlStatement}", ex);
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
