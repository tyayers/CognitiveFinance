using System;
using System.Collections.Generic;
using System.Text;

namespace CogStockFunctions.Dtos
{
    public class TweetSummary
    {
        public string CompanyName { get; set; }
        public int TweetCount { get; set; }
        public int TweetLikes { get; set; }
        public int TweetRetweets { get; set; }
        public Dictionary<string, Tweet> Tweets { get; set; } = new Dictionary<string, Tweet>();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
