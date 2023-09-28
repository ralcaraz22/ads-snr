﻿using ads.Data;
using ads.Interface;
using ads.Models.Data;
using ads.Utility;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace ads.Repository
{
    public class SalesRepo : ISales
    {
        private readonly IOpenQuery _openQuery;
        private readonly ILogs _logs;

        private readonly DateConvertion dateConvertion = new DateConvertion();
        private List<DataRows> _saleList = new List<DataRows>();

        public SalesRepo(IOpenQuery openQuery, ILogs logs)
        {
            _openQuery = openQuery;
            _logs = logs;
        }

        private List<DataRows> GenerateListOfDataRows(List<GeneralModel> datas, string sku, bool hasSales, bool useStartDate, string? date)
        {
            List<DataRows> listOfOledb = new List<DataRows>();

            foreach (var data in datas)
            {
                var Olde = new DataRows
                {
                    Sku = sku,
                    Clubs = data.CSSTOR ?? data.ISTORE,
                    Sales = hasSales ? data.CSQTY : 0,
                    Date = dateConvertion.ConvertStringDate(!useStartDate ? data.CSDATE : date),
                };

                listOfOledb.Add(Olde);
            }

            return listOfOledb;
        }

        //Get Sales
        public async Task<List<DataRows>> GetSalesAsync(string start, string end, List<GeneralModel> skus, List<GeneralModel> listOfSales, List<GeneralModel> inventories)
        {
            List<DataRows> transformedData = new List<DataRows>();

            List<DataRows> listOfOledb = new List<DataRows>();

            List<DataRows> listOfTBLSTR = new List<DataRows>();

            List<DataRows> listOfINVMST = new List<DataRows>();

            List<Logging> Log = new List<Logging>();

            DateTime startLogs = DateTime.Now;

            try
            {
                //OleDb Select Query
                using (OledbCon db = new OledbCon())
                {
                    if (db.Con.State == ConnectionState.Closed)
                    {
                        db.Con.Open();
                    }

                    var inventoryLookup = inventories.GroupBy(x => x.INUMBR2).ToDictionary(group => group.Key, group => group.ToList());
                    var salesLookup = listOfSales.GroupBy(x => x.CSSKU).ToDictionary(group => group.Key, group => group.ToList());

                    foreach (var sku in skus)
                    {
                        if (salesLookup.TryGetValue(sku.INUMBR, out var salesOut))
                        {
                            foreach (var data in salesOut)
                            {
                                var Olde = new DataRows
                                {
                                    Sku = sku.INUMBR,
                                    Clubs = data.CSSTOR,
                                    Sales = data.CSQTY,
                                    Date = dateConvertion.ConvertStringDate( data.CSDATE),
                                };

                                listOfOledb.Add(Olde);
                            }
                            //var generatedList = GenerateListOfDataRows(salesOut, sku.INUMBR, false, false, start);

                            //listOfOledb.AddRange(generatedList);
                        }
                        else
                        {
                            if (inventoryLookup.TryGetValue(sku.INUMBR, out var inventoryOut))
                            {
                                //var generatedList = GenerateListOfDataRows(inventoryOut, sku.INUMBR, true, true, start);
                                foreach (var data in inventoryOut)
                                {
                                    var Olde = new DataRows
                                    {
                                        Sku = sku.INUMBR,
                                        Clubs = data.ISTORE,
                                        Sales = 0,
                                        Date = dateConvertion.ConvertStringDate(start),
                                    };

                                    listOfOledb.Add(Olde);
                                }
                                //listOfOledb.AddRange(generatedList);
                            }
                            else
                            {
                                // this entry is in the master list of sku, but not yet part of sales and enventory table
                                // this is used when computing chain/all store ADS
                                // this is filtered out when computing ADS of per store per sku
                                var Olde = new DataRows
                                {
                                    Sku = sku.INUMBR,
                                    Clubs = string.Empty,
                                    Sales = 0,
                                    Date = dateConvertion.ConvertStringDate(start),
                                };

                                listOfOledb.Add(Olde);
                            }
                        }
                    }

                    //Bluk insert in tbl_Data table
                    using (var transaction = db.Con.BeginTransaction())
                    {
                        using (var bulkCopy = new SqlBulkCopy(db.Con, SqlBulkCopyOptions.Default, transaction))
                        {
                            bulkCopy.DestinationTableName = "tbl_data";
                            bulkCopy.BatchSize = 1000;

                            var dataTable = new DataTable();
                            dataTable.Columns.Add("Id", typeof(int));
                            dataTable.Columns.Add("Clubs", typeof(string));
                            dataTable.Columns.Add("Sku", typeof(string));
                            //dataTable.Columns.Add("Inventory", typeof(decimal));
                            dataTable.Columns.Add("Sales", typeof(decimal));
                            dataTable.Columns.Add("Date", typeof(DateTime));

                            foreach (var rowData in listOfOledb)
                            {
                                var row = dataTable.NewRow();
                                row["Clubs"] = rowData.Clubs;
                                row["Sku"] = rowData.Sku;
                                //row["Inventory"] = rowData.Inventory;
                                row["Sales"] = rowData.Sales;
                                row["Date"] = rowData.Date;
                                dataTable.Rows.Add(row);
                            }
                            await bulkCopy.WriteToServerAsync(dataTable);
                        }

                        transaction.Commit();
                    }
                }

                DateTime endLogs = DateTime.Now;
                Log.Add(new Logging
                {
                    StartLog = startLogs,
                    EndLog = endLogs,
                    Action = "Sales",
                    Message = "Total Rows Inserted : " + listOfOledb.Count + "",
                    Record_Date = start
                });

                _logs.InsertLogs(Log);

                return listOfOledb;
            }
            catch (Exception e)
            {
                DateTime endLogs = DateTime.Now;
                Log.Add(new Logging
                {
                    StartLog = startLogs,
                    EndLog = endLogs,
                    Action = "Error",
                    Message = "Sales : " + e.Message + " : Date : " + start + "",
                    Record_Date = start
                });

                _logs.InsertLogs(Log);

                return listOfOledb;
            }


        }

        public async Task GetAllSales(string dateListString, int pageSize, int offset, OledbCon db)
        {
            try
            {
                await Task.Run(() =>
                {
/*                    string query = "select * from tbl_data where Date in (" + dateListString + ") " +
                        "ORDER BY Date OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY ";*/

                    string strConn = "data source='199.84.0.201';Initial Catalog=ADS.UAT;User Id=sa;password=@dm1n@8800;Trusted_Connection=false;MultipleActiveResultSets=true;TrustServerCertificate=True;";
                    var con = new SqlConnection(strConn);

                    using (var command = new SqlCommand("_sp_GetTblDataSample", con))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@Offset", offset);
                        command.Parameters.AddWithValue("@PageSize", pageSize);
                        command.Parameters.AddWithValue("@dateListString", dateListString);
                        command.CommandTimeout = 18000;
                        con.Open();

                        // Open the connection and execute the command
                        SqlDataReader reader = command.ExecuteReader();

                        // Process the result set
                        while (reader.Read())
                        {

                            DataRows Olde = new DataRows
                            {
                                Clubs = reader["Clubs"].ToString(),
                                Sku = reader["Sku"].ToString(),
                                Sales = Convert.ToDecimal(reader["Sales"].ToString()),
                                Date = reader.GetDateTime("Date"),
                            };

                            _saleList.Add(Olde);
                        }

                        // Close the reader and connection
                        reader.Close();
                        con.Close();
                    }
                });

            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
        }

        //list Data
        public async Task<List<DataRows>> ListSales(string dateListString, OledbCon db)
        {
            List<DataRows> list = new List<DataRows>();
            var tasks = new List<Task>();
      /*      var pageSize = 5000*/;

            // Get the total count of rows for your date filter
            var rowCount = await CountSales(dateListString, db);
            var pageSize = (int)Math.Ceiling((double)rowCount /5);

            // Calculate the total number of pages
            var totalPages = (int)Math.Ceiling((double)rowCount / pageSize);

            for (int pageNumber = 0; pageNumber < totalPages; pageNumber++)
            {
                int offset = pageSize * pageNumber;

                tasks.Add(GetAllSales(dateListString.Replace("'", ""), pageSize, offset, db));
            }

            await Task.WhenAll(tasks);

            return _saleList;
        }

        //Total Sales
        public string TotalSales(string startDate, string endDate)
        {
            var totalsales = "";
            using (MsSqlCon db = new MsSqlCon())
            {
                string oledb = "Select Count(*) Total from TBL_DATA where Date between @StartDate and @EndDate";

                using (SqlCommand cmd = new SqlCommand(oledb, db.Con))
                {
                    cmd.Parameters.AddWithValue("@StartDate", startDate);
                    cmd.Parameters.AddWithValue("@EndDate", endDate);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            totalsales += reader["Total"].ToString();
                        }
                    }
                }
            }

            return totalsales.ToString();
        }

        //TotalCount of Sales
        public async Task<int> CountSales(string dateListString, OledbCon db)
        {
            int totalCount = 0;

            string query = "select COUNT(*) as Count from tbl_data where Date in (" + dateListString + ") ";

            using (SqlCommand cmd = new SqlCommand(query, db.Con))
            {
                cmd.CommandTimeout = 18000;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (reader.Read())
                    {
                        totalCount = (int)reader["Count"];
                    }
                }
            }

            return totalCount;
        }

    }
}
