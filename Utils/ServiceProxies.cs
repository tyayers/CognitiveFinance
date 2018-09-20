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

namespace CogStockFunctions.Utils
{
    public static class ServiceProxies
    {
        private static AuthenticationResponse _twitterAuth = GetTwitterAuthenticationToken();
        public static List<News> GetNews(string Category, TraceWriter log)
        {
            List<News> results = new List<News>();

            using (HttpClient client = new HttpClient())
            {
                string subKey = System.Environment.GetEnvironmentVariable("BingKey");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subKey);
                string rssContent = client.GetStringAsync("https://api.cognitive.microsoft.com/bing/v7.0/news?category=technology&mkt=en-us").Result;

                Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(rssContent);

                if (obj != null) {
                    //AuthenticationResponse auth = GetTwitterAuthenticationToken();

                    foreach (JObject newsItem in obj["value"]) {
                        string title = newsItem["name"].ToString();
                        string description = newsItem["description"].ToString();
                        string datePublished = newsItem["datePublished"].ToString();
                        string sentiment = GetSentitment(title + " " + description);

                        List<Company> companies = GetCompanies(title, log);

                        if (companies != null) {
                            foreach (Company comp in companies) {
                                string publishDate = newsItem["datePublished"].ToString();
                                DateTime publishDateTime = DateTime.Parse(publishDate);
                                string Price = GetStockPrice(comp.Symbol, publishDateTime.ToString("yyyy-MM-dd"), log);
                                News newNews = new News(){
                                    CompanyName = comp.Name,
                                    Title = newsItem["name"].ToString(),
                                    Description = newsItem["description"].ToString(),
                                    PublishDate = publishDate,
                                    Sentiment =  GetSentitment(newsItem["name"].ToString() + " " + newsItem["description"]),
                                    Symbol = comp.Symbol,
                                    Price = Price
                                };

                                results.Add(newNews);
                            }
                        }
                    }
                }

                return results;
            }
        }

        public static List<Company> GetCompanies(string Text, TraceWriter log) {
            List<Company> results = new List<Company>();

            using (HttpClient client = new HttpClient())
            {
                string payload = "{\"documents\": [ { \"id\": \"1\", \"text\": \"" + Text + "\"}]}";
                string textAnalyticsKey = System.Environment.GetEnvironmentVariable("TextAnalyticsKey");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", textAnalyticsKey);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                HttpResponseMessage msg = client.PostAsync("https://westeurope.api.cognitive.microsoft.com/text/analytics/v2.0/entities", content).Result;
                if (msg.IsSuccessStatusCode)
                {
                    var JsonDataResponse = msg.Content.ReadAsStringAsync().Result;
                    Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(JsonDataResponse);

                    if (obj != null) {
                        foreach (JObject docObject in obj["documents"]) {

                            foreach (JObject entityObject in docObject["entities"]) {
                                string name = entityObject["name"].ToString().Replace(" (company)", "");

                                Company newCompany = GetCompanyInfo(name, log);
                                if (newCompany.Type == "Organization" && !newCompany.Blacklisted)
                                    results.Add(newCompany);
                            }
                        }
                    }
                }                
            }

            return results;
        }

        public static string GetEntityType(string Name) {
            string result = "Unknown";
            using (HttpClient client = new HttpClient())
            {
                string textAnalyticsKey = System.Environment.GetEnvironmentVariable("SearchKey");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", textAnalyticsKey);
                string response = client.GetStringAsync("https://api.cognitive.microsoft.com/bing/v7.0/entities/?q=" + Name + "&mkt=en-us&count=10&offset=0&safesearch=Moderate").Result;

                Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(response);

                if (obj != null && obj["entities"] != null) {
                    result = obj["entities"]["value"].First["entityPresentationInfo"]["entityTypeHints"].First.ToString();
                }
            }
           
           return result;
        }

        public static Company GetCompanyInfo(string CompanyName, TraceWriter log) {
            Company newCompany = new Company();
            string sqlStatement = $"SELECT * FROM Companies WHERE Name = N'{CompanyName}'";
            // First check if company already exists in db, then just use that

            try {
                string conString = System.Environment.GetEnvironmentVariable("ConnectionString");
                using (var connection = new SqlConnection(conString))
                {
                    var command = new SqlCommand(sqlStatement, connection);
                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        newCompany.Name = (string) reader["Name"];
                        newCompany.Symbol = (string) reader["Symbol"];
                        newCompany.Blacklisted = (bool) reader["Blacklisted"];
                        newCompany.Type = "Organization";
                    }

                    // Call Close when done reading.
                    reader.Close();   
                }
            }
            catch (Exception ex) {
                log.Error($"GetCompanyInfo db error with {sqlStatement}", ex);
            }

            // In case doesn't exist in db, try from the internet
            if (String.IsNullOrEmpty(newCompany.Name)) {
                newCompany.Name = CompanyName;
                newCompany.Symbol = GetCompanyStockSymbol(CompanyName);
                newCompany.Type = GetEntityType(CompanyName); 
            }

            return newCompany;
        }

        public static string GetCompanyStockSymbol(string CompanyName) {
            string result = "";
            using (HttpClient client = new HttpClient())
            {
                string stockContent = client.GetStringAsync("http://d.yimg.com/autoc.finance.yahoo.com/autoc?query=" + CompanyName + "&lang=eng&callback=YAHOO.Finance.SymbolSuggest.ssCallback").Result.Replace("YAHOO.Finance.SymbolSuggest.ssCallback(", "").Replace(");", "");
                Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(stockContent);

                if (obj != null && obj["ResultSet"] != null && obj["ResultSet"]["Result"] != null) {
                    foreach (JObject stockItem in obj["ResultSet"]["Result"]) {
                        string stockExchange = stockItem["exchDisp"].ToString();
                        if (stockExchange == "NASDAQ" || stockExchange == "NYSE" || stockExchange == "TLX Exchange") {
                            // We have a good match
                            result = stockItem["symbol"].ToString();
                        }
                    }
                }
            }
            return result;            
        }  

        public static string GetSentitment(string Text) {
            string result = "-1";

            using (HttpClient client = new HttpClient())
            {
                string payload = "{\"documents\": [ { \"id\": \"1\", \"language\": \"en\", \"text\": \"" + Text + "\"}]}";
                string sentKey = System.Environment.GetEnvironmentVariable("SentimentKey");
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", sentKey);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                HttpResponseMessage msg = client.PostAsync("https://westeurope.api.cognitive.microsoft.com/text/analytics/v2.0/sentiment", content).Result;
                if (msg.IsSuccessStatusCode)
                {
                    var JsonDataResponse = msg.Content.ReadAsStringAsync().Result;
                    Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(JsonDataResponse);

                    if (obj != null) {
                        foreach (JObject docObject in obj["documents"]) {
                            result = docObject["score"].ToString();
                            result = Convert.ToDecimal(result).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }
                }                
            }

            return result;
        }     

        public static List<Tweet> SearchTweets(string Company) {
            List<Tweet> results = new List<Tweet>();

            using (HttpClient client = new HttpClient())
            {
                //client.DefaultRequestHeaders.Add("Authorization", "OAuth oauth_consumer_key=\"fcoIK7zRH6H3IkhS6WogDiBYd\",oauth_signature_method=\"HMAC-SHA1\",oauth_timestamp=\"1530618148\",oauth_nonce=\"cDgNDFkNOnd\",oauth_version=\"1.0\",oauth_signature=\"MthdIYwP3fV32L4qPCUkX3Nn%2BXc%3D\"");
                client.DefaultRequestHeaders.Add("Authorization", string.Format("Bearer {0}", _twitterAuth.AccessToken));

                client.DefaultRequestHeaders.Add("user-agent", "node.js");
                string stockContent = client.GetStringAsync("https://api.twitter.com/1.1/search/tweets.json?q=" + Company + "&result_type=recent&lang=en&count=100").Result;
                Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(stockContent);

                if (obj != null && obj["statuses"] != null) {
                    foreach (JObject tweet in obj["statuses"]) {
                        Tweet newTweet = new Tweet() {
                            Text = tweet["text"].ToString(),
                            FavoriteCount = tweet["favorite_count"].ToString(),
                            RetweetCount = tweet["retweet_count"].ToString(),
                            Sentiment = "-1",//GetSentitment(tweet["text"].ToString()),
                            DatePublished = tweet["created_at"].ToString()
                        };

                        results.Add(newTweet);                        
                    }
                }
            }

            return results;  
        }

        public static string GetStockPrice(string Symbol, string Date, TraceWriter log) {
            string result = "0";

            try {
                if (Symbol != "") {
                    using (HttpClient client = new HttpClient())
                    {
                        string stockKey = System.Environment.GetEnvironmentVariable("StockKey");
                        HttpResponseMessage msg = client.GetAsync("https://www.alphavantage.co/query?function=TIME_SERIES_DAILY_ADJUSTED&symbol=" + Symbol + "&apikey=" + stockKey).Result;
                        if (msg.IsSuccessStatusCode)
                        {
                            var JsonDataResponse = msg.Content.ReadAsStringAsync().Result;
                            Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(JsonDataResponse);

                            if (obj != null && obj["Time Series (Daily)"] != null) {
                                JToken dayPrice = obj["Time Series (Daily)"].First;

                                if (Date != "") {
                                    // We are looking for a specific day, so select for that
                                    dayPrice = obj["Time Series (Daily)"][Date];
                                }

                                if (dayPrice != null) {
                                    JToken closePrice = dayPrice["4. close"];
                                    result = closePrice.ToString();
                                }
                                //result = Convert.ToDecimal(result).ToString(System.Globalization.CultureInfo.InvariantCulture);
                            }
                        }                
                    }
                }
            }
            catch (Exception ex) {
                log.Error ("GetStockPrice error", ex);
            }

            return result;           
        }

        public static GitHub GetGitHubStars(string Company) {
            GitHub result = new GitHub();

            if (Company != "") {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("user-agent", "node.js");
                    string gitHubUser = System.Environment.GetEnvironmentVariable("GitHubUser");
                    string gitHubKey = System.Environment.GetEnvironmentVariable("GitHubKey");
                    HttpResponseMessage msg = client.GetAsync("https://api.github.com/search/repositories?q=" + Company + "/&sort=stars&order=desc&client_id=" + gitHubUser + "&client_secret=" + gitHubKey).Result;
                    if (msg.IsSuccessStatusCode)
                    {
                        var JsonDataResponse = msg.Content.ReadAsStringAsync().Result;
                        Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(JsonDataResponse);

                        foreach (JObject repo in obj["items"]) {
                            result.Stars += Convert.ToInt32(repo["stargazers_count"].ToString());
                            result.Watches += Convert.ToInt32(repo["watchers_count"].ToString());
                        }
                    }                
                }
            }

            return result;    
        }

        public static AuthenticationResponse GetTwitterAuthenticationToken()
        {
            var client = new HttpClient();
            var uri = new Uri("https://api.twitter.com/oauth2/token");

            string twitterUser = System.Environment.GetEnvironmentVariable("TwitterUser");
            string twitterKey = System.Environment.GetEnvironmentVariable("TwitterKey");
            var encodedConsumerKey = WebUtility.UrlEncode(twitterUser);
            var encodedConsumerSecret = WebUtility.UrlEncode(twitterKey);
            var combinedKeys = String.Format("{0}:{1}", encodedConsumerKey, encodedConsumerSecret);
            var utfBytes = System.Text.Encoding.UTF8.GetBytes(combinedKeys);
            var encodedString = Convert.ToBase64String(utfBytes);
            client.DefaultRequestHeaders.Add("Authorization", string.Format("Basic {0}", encodedString));

            var data = new List<KeyValuePair<string, string>> 
            { 
                new KeyValuePair<string, string>("grant_type", "client_credentials") 
            };

            var postData = new FormUrlEncodedContent(data);
            var response = client.PostAsync(uri, postData).Result;
            AuthenticationResponse authenticationResponse;
            using (response)
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new Exception("Authorization token with Twitter did not work!");

                var content = response.Content.ReadAsStringAsync().Result;
                authenticationResponse = JsonConvert.DeserializeObject<AuthenticationResponse>(content);

                if (authenticationResponse.TokenType != "bearer")
                    throw new Exception("wrong result type");
            }

            return authenticationResponse;
        }                                             
    }

    public class AuthenticationResponse
    {
        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
    }      
}