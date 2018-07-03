using System;
using System.Collections.Generic;
using System.Text;

namespace CogStockFunctions.Dtos
{
    public class Tweet
    {
        public string Text { get; set; }
        public string FavoriteCount { get; set; }
        public string RetweetCount { get; set; }
        public string Sentiment { get; set; }
        public string DatePublished {get;set;}
    }
}
