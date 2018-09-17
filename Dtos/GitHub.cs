using System;
using System.Collections.Generic;
using System.Text;
using NPoco;


namespace CogStockFunctions.Dtos
{
    public class GitHub
    {
        public string CompanyName {get;set;}
        public Int32 Stars { get; set; } = 0;
        public Int32 Watches { get; set; } = 0;
    }
}
