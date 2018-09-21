using NPoco;
using System;
using System.Collections.Generic;
using System.Text;

namespace CogStockFunctions.Dtos
{
    [TableName("DailyUpdate")]
    [PrimaryKey("Id")]
    public class DailyUpdate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
        public string StockPrice { get; set; }
        public int TweetCount { get; set; }
        public int TweetRetweets { get; set; }
        public int TweetLikes { get; set; }
        public int GitHubStars { get; set; }
        public int GitHubWatchers { get; set; }
        public int JobPostings { get; set; }
        public int NewsStories { get; set; }
        public DateTime Date { get; set; }
    }
}
