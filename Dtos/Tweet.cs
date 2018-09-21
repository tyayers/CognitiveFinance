using System;
using System.Collections.Generic;
using System.Text;

namespace CogStockFunctions.Dtos
{
    public class Tweet
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public int FavoriteCount { get; set; }
        public int RetweetCount { get; set; }
        public int Sentiment { get; set; }
        public string DatePublished {get;set;}
    }
}
