using CogStockFunctions.Dtos;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace CogStockFunctions.Utils
{
    public static class TwitterUtils
    {
        public static TweetSummary GetTweetSummary(string CompanyName, TraceWriter log)
        {
            TweetSummary result = new TweetSummary() { CompanyName = CompanyName };

            List<Tweet> tweets = ServiceProxies.SearchTweets(CompanyName);
            log.Info($"Found {tweets.Count.ToString()} tweets for company {CompanyName}");
            foreach (Tweet tweet in tweets)
            {
                DateTime publishDateTime = DateTime.ParseExact(tweet.DatePublished, "ddd MMM dd HH:mm:ss zzz yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal);
                
                // Set Start and End dates, if appropriate
                if (result.StartDate == DateTime.MinValue || publishDateTime < result.StartDate) result.StartDate = publishDateTime;
                if (result.EndDate == DateTime.MinValue || publishDateTime > result.EndDate) result.EndDate = publishDateTime;

                if (result.Tweets.ContainsKey(tweet.Id))
                {
                    if (tweet.RetweetCount > result.Tweets[tweet.Id].RetweetCount) result.Tweets[tweet.Id].RetweetCount = tweet.RetweetCount;
                    if (tweet.FavoriteCount > result.Tweets[tweet.Id].FavoriteCount) result.Tweets[tweet.Id].FavoriteCount = tweet.FavoriteCount;
                }
                else
                    result.Tweets.Add(tweet.Id, tweet);
            }

            result.TweetCount = result.Tweets.Count;
            
            foreach (Tweet tweet in result.Tweets.Values)
            {
                result.TweetLikes += tweet.FavoriteCount;
                result.TweetRetweets += tweet.RetweetCount;
            }

            return result;
        }
    }
}
