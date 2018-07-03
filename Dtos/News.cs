using System;
using System.Collections.Generic;
using System.Text;

namespace CogStockFunctions.Dtos
{
    public class News
    {
        public string CompanyName { get; set; }
        public string Title { get; set; }
        public string Description {get;set;}
        public string PublishDate {get;set;}
        public string Sentiment { get; set; }
        public string Symbol {get;set;}
        public string Price {get;set;}

    }
}
