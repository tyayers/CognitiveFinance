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
using NPoco;

namespace CogStockFunctions.Utils
{
    public static class DataRepository 
    {
        public static List<Company> GetCompaniesFromDb(TraceWriter log) {
            List<Company> companies = new List<Company>();

            string sqlStatement = $"SELECT * FROM Companies WHERE Symbol <> N'' AND Blacklisted = 0";
            // First check if company already exists in db, then just use that

            try {
                string conString = System.Environment.GetEnvironmentVariable("ConnectionString");
                using (var connection = new SqlConnection(conString))
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

        public static int CleanBlacklistedCompanies(TraceWriter log) {
            int result = 0;

            string sqlStatement = $"DELETE FROM Updates WHERE Updates.Name IN (SELECT Companies.Name FROM Companies WHERE Blacklisted=1)";
            // First check if company already exists in db, then just use that

            try {
                string conString = System.Environment.GetEnvironmentVariable("ConnectionString");

                using (var connection = new SqlConnection(conString))
                {
                    var command = new SqlCommand(sqlStatement, connection);
                    connection.Open();
                    result = command.ExecuteNonQuery();
                }
            }
            catch (Exception ex) {
                log.Error($"CleanBlacklistedCompanies db error with {sqlStatement}", ex);
            }

            return result;
        }        

        public static void AddUpdate(string Id, string Name, string Symbol, string Type, string Text, int Metric, int Metric2, string Sentiment, string StockPrice, DateTime DatePublished, TraceWriter log) {
            string sqlStatement = $"IF NOT EXISTS (SELECT * FROM Updates WHERE Id=N'{Id}') BEGIN INSERT INTO Updates (Id, Name, Symbol, Type, Text, Metric, Metric2, Sentiment, StockPrice, LastUpdate) VALUES (N'{Id}', N'{Name}', N'{Symbol}', '{Type}', N'{Text.Replace("'", "").Replace("\"", "")}', {Metric}, {Metric2}, {Sentiment}, {StockPrice}, '{DatePublished.ToString("yyyy-MM-dd HH:mm:ss")}') END";

            try {
                string conString = System.Environment.GetEnvironmentVariable("ConnectionString");
                using (var connection = new SqlConnection(conString))
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

        public static void AddDailyUpdate(DailyUpdate update)
        {
            SqlConnection con = null;

            try
            {
                con = new SqlConnection(System.Environment.GetEnvironmentVariable("ConnectionString"));
                con.Open();
                using (var db = new Database(con))
                {
                    db.Insert(update);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("NosyRepo error in InsertStory. " + ex.ToString());
            }
            finally
            {
                if (con != null) con.Close();
            }
        }
    }
}