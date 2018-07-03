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
        public static List<News> GetNews()
        {
            List<News> results = new List<News>();

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "170b9892d8cc491e90401b645cc0b8af");
                string rssContent = client.GetStringAsync("https://api.cognitive.microsoft.com/bing/v7.0/news?category=technology&mkt=en-us").Result;

                Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(rssContent);

                if (obj != null) {
                    //AuthenticationResponse auth = GetTwitterAuthenticationToken();
            
                    foreach (JObject newsItem in obj["value"]) {
                        string title = newsItem["name"].ToString();
                        string description = newsItem["description"].ToString();
                        string datePublished = newsItem["datePublished"].ToString();
                        string sentiment = GetSentitment(title + " " + description);

                        List<Company> companies = GetCompanies(title);

                        if (companies != null) {
                            foreach (Company comp in companies) {
                                string Price = GetStockPrice(comp.Symbol);
                                News newNews = new News(){
                                    CompanyName = comp.Name,
                                    Title = newsItem["name"].ToString(),
                                    Description = newsItem["description"].ToString(),
                                    PublishDate = newsItem["datePublished"].ToString(),
                                    Sentiment =  GetSentitment(newsItem["name"].ToString() + " " + newsItem["description"]),
                                    Symbol = comp.Symbol,
                                    Price = Price
                                };

                                results.Add(newNews);
                                // if (!CheckIfCompanyExists(comp.Name)) {
                                //     AddCompany(comp.Name, comp.Symbol);
                                //     // Add tweets
                                //     if (comp.Symbol != "" && Price != "0") {
                                //         List<Tweet> tweets = SearchTweets(comp.Name, auth);
                                //         foreach (Tweet tweet in tweets) {
                                //             AddUpdate(comp.Name + "_TWEET_" + datePublished, comp.Name, comp.Symbol, "TWEET", tweet.Text, Convert.ToInt32(tweet.FavoriteCount), Convert.ToInt32(tweet.RetweetCount), tweet.Sentiment, Price);
                                //         }
                                //     }
                                // }
                                // if (comp.Symbol != "" && Price != "0") {
                                //     // Add news update                                                                                                
                                //     AddUpdate(comp.Name + "_" + datePublished, comp.Name, comp.Symbol, "NEWS", title, 0, 0, sentiment, Price);
                                //     // Add github stars
                                //     int gitHubStars = GetGitHubStars(comp.Name);
                                //     if (gitHubStars > 0) {
                                //         AddUpdate(comp.Name + "_GHSTARS_" + datePublished, comp.Name, comp.Symbol, "GHSTARS", "", gitHubStars, 0, "-1", Price);
                                //     }
                                // }
                            }
                        }
                    }
                }

                return results;
            }
        }

        public static List<Company> GetCompanies(string Text) {
            List<Company> results = new List<Company>();

            using (HttpClient client = new HttpClient())
            {
                string payload = "{\"documents\": [ { \"id\": \"1\", \"text\": \"" + Text + "\"}]}";
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "d172712b5fb94fd1aeb43e86517f79ff");
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
                                string symbol = GetCompanyStockSymbol(name);

                                results.Add(new Company(){ Name=name, Symbol=symbol});
                            }
                        }
                    }
                }                
            }

            return results;
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
                        if (stockExchange == "NASDAQ" || stockExchange == "NYSE") {
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
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "d172712b5fb94fd1aeb43e86517f79ff");
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
                string stockContent = client.GetStringAsync("https://api.twitter.com/1.1/search/tweets.json?q=" + Company + "&result_type=popular").Result;
                Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(stockContent);

                if (obj != null && obj["statuses"] != null) {
                    foreach (JObject tweet in obj["statuses"]) {
                        Tweet newTweet = new Tweet() {
                            Text = tweet["text"].ToString(),
                            FavoriteCount = tweet["favorite_count"].ToString(),
                            RetweetCount = tweet["retweet_count"].ToString(),
                            Sentiment = GetSentitment(tweet["text"].ToString()),
                            DatePublished = tweet["created_at"].ToString()
                        };

                        results.Add(newTweet);                        
                    }
                }
            }

            return results;  
        }

        public static string GetStockPrice(string Symbol) {
            string result = "0";

            if (Symbol != "") {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage msg = client.GetAsync("https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol=" + Symbol + "&interval=1min&apikey=FNT06P1FPP3XUTCW").Result;
                    if (msg.IsSuccessStatusCode)
                    {
                        var JsonDataResponse = msg.Content.ReadAsStringAsync().Result;
                        Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(JsonDataResponse);

                        if (obj != null && obj["Time Series (1min)"] != null) {
                            JToken lastPrice = obj["Time Series (1min)"].First;
                            JToken closePrice = lastPrice.First["4. close"];
                            result = closePrice.ToString();
                            //result = Convert.ToDecimal(result).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }                
                }
            }

            return result;           
        }

        public static GitHub GetGitHubStars(string Company) {
            GitHub result = new GitHub();

            if (Company != "") {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("user-agent", "node.js");
                    HttpResponseMessage msg = client.GetAsync("https://api.github.com/search/repositories?q=" + Company + "/&sort=stars&order=desc&client_id=63b1752838e79e37045863b1752838e79e370458&client_secret=688e260b84b06028baf8c1d1011c2400670fb449").Result;
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

            var encodedConsumerKey = WebUtility.UrlEncode("fcoIK7zRH6H3IkhS6WogDiBYd");
            var encodedConsumerSecret = WebUtility.UrlEncode("WzAduP0pcuirkBd17xVoV5d7PlAK9qh6usfIhiuU7FgFC7hms2");
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