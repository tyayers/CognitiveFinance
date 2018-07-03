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
    public static class DataRepository 
    {
        public static List<Company> GetCompaniesFromDb(TraceWriter log) {
            List<Company> companies = new List<Company>();

            string sqlStatement = $"SELECT * FROM Companies WHERE Symbol <> N''";
            // First check if company already exists in db, then just use that

            try {
                using (var connection = new SqlConnection("Server=tcp:clashserver.database.windows.net,1433;Initial Catalog=clashofaisql;Persist Security Info=False;User ID=clashuser;Password=Passwort123!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"))
                {
                    var command = new SqlCommand(sqlStatement, connection);
                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Company newCompany = new Company();
                        newCompany.Name = (string) reader["Name"];
                        newCompany.Symbol = (string) reader["Symbol"];
                        newCompany.Type = "Organization";

                        companies.Add(newCompany);
                    }

                    // Call Close when done reading.
                    reader.Close();   
                }
            }
            catch (Exception ex) {
                log.Error($"GetCompaniesFromDb db error with {sqlStatement}", ex);
            }

            return companies;
        }

        public static void AddUpdate(string Id, string Name, string Symbol, string Type, string Text, int Metric, int Metric2, string Sentiment, string StockPrice, DateTime DatePublished, TraceWriter log) {
            string sqlStatement = $"IF NOT EXISTS (SELECT * FROM Updates WHERE Id=N'{Id}') BEGIN INSERT INTO Updates (Id, Name, Symbol, Type, Text, Metric, Metric2, Sentiment, StockPrice, LastUpdate) VALUES (N'{Id}', N'{Name}', N'{Symbol}', '{Type}', N'{Text.Replace("'", "").Replace("\"", "")}', {Metric}, {Metric2}, {Sentiment}, {StockPrice}, '{DatePublished.ToString("yyyy-MM-dd HH:mm:ss")}') END";

            try {
                using (var connection = new SqlConnection("Server=tcp:clashserver.database.windows.net,1433;Initial Catalog=clashofaisql;Persist Security Info=False;User ID=clashuser;Password=Passwort123!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"))
                {
                    var command = new SqlCommand(sqlStatement, connection);
                    connection.Open();
                    command.ExecuteNonQuery();
                }  
            }
            catch (Exception ex) {
                log.Error($"AddUpdate error executing SQL Statement {sqlStatement}", ex);
            }
        } 

    }
}