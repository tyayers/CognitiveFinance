using CogStockFunctions.Dtos;
using CogStockFunctions.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Generic;
using System.Text;

namespace CogStockFunctions
{
    public static class DailyUpdateTimer
    {
        [FunctionName("DailyUpdateTimer")]
        public static void Run([TimerTrigger("0 0 5 * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info("C# DailyUpdateTimer timer trigger function processed a request.");
            string result = DailyUpdater.Update(log);
            log.Info("C# DailyUpdateTimer timer completed: " + result);
        }
    }

    public static class DailyUpdateTrigger
    {
        [FunctionName("DailyUpdateTrigger")]
        public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("C# DailyUpdateTrigger HTTP trigger function processed a request.");
            string result = DailyUpdater.Update(log);
            return new OkObjectResult(result);
        }
    }

    public static class DailyUpdater
    {
        public static string Update(TraceWriter log)
        {
            string result = "Daily update complete, nothing unusual to report..";

            try
            {
                List<Company> companies = Utils.DataRepository.GetCompaniesFromDb(log);

                foreach (Company company in companies)
                {
                    if (!String.IsNullOrEmpty(company.Symbol))
                    {
                        DailyUpdate update = new DailyUpdate() { Name = company.Name, Symbol = company.Symbol, Date = DateTime.Now };
                        //update.Id = company.Name + "_" + update.Date.ToString("yyyyMMdd");
                        //update.Id = update.Id.Replace(" ", "_");

                        TweetSummary tweets = TwitterUtils.GetTweetSummary(company.Name, log);
                        update.TweetCount = tweets.TweetCount;
                        update.TweetLikes = tweets.TweetLikes;
                        update.TweetRetweets = tweets.TweetRetweets;
                        update.StockPrice = ServiceProxies.GetStockPrice(company.Symbol, "", log);
                        GitHub ghub = ServiceProxies.GetGitHubStars(company.Name);
                        if (ghub != null)
                        {
                            update.GitHubStars = ghub.Stars;
                            update.GitHubWatchers = ghub.Watches;
                        }

                        List<News> news = ServiceProxies.SearchNews(company.Name, log);
                        update.NewsStories = news.Count;

                        DataRepository.AddDailyUpdate(update);

                        // Sleep to not overload free APIs
                        System.Threading.Thread.Sleep(12000);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message, ex);
                result = "DailyUpdater Error: " + ex.ToString();
            }
                       
            return result;
        }
    }
}
