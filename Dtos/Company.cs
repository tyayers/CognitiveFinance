using System;
using System.Collections.Generic;
using System.Text;
using NPoco;

namespace CogStockFunctions.Dtos
{
    [TableName("Companies")]
    [PrimaryKey("Name")]
    public class Company
    {
        public string Name { get; set; }
        public string Symbol { get; set; }
        public string LastUpdate{ get; set; }
        public string Type {get;set;}
        public bool Blacklisted {get;set;} = false;
    }
}
