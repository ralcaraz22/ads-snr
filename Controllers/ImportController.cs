﻿using ads.Data;
using ads.Interface;
using ads.Models.Data;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ads.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("AllowOrigin")]
    public class ImportController : ControllerBase
    {
        private readonly IInvetory _invetory;
        private readonly ISales _sales;
        private readonly IAds _ads;
        private readonly IOpenQuery _openQuery;
        private readonly IInventoryBackup _inventoryBackup;
        private readonly IItem _item;

        public ImportController(IInvetory invetory, ISales sales, IAds ads, IOpenQuery openQuery, IInventoryBackup inventoryBackup, IItem item)
        {
            _invetory = invetory;
            _sales = sales;
            _ads = ads;
            _openQuery = openQuery;
            _inventoryBackup = inventoryBackup;
            _item = item;
        }

        [HttpPost]
        [Route("ImportInventoryAndSales")]
        public async Task<IActionResult> Import(List<string> dates)
        {
            //foreach (var item in dates)
            //{
            //    await _invetory.GetInventoryAsync(item, item);
            //     await _sales.GetSalesAsync(item, item);

            //}

            await _ads.ComputeAds();

            return Ok();
        }

        [HttpPost]
        [Route("GetInventory")]
        public async Task<IActionResult> GetInventory(string dates)
        {
            using (OledbCon db = new OledbCon())
            {
                await db.OpenAsync();

                //list of Inventory within 56 days in Local DB
                var listInventoryResult = await _invetory.ListInv(dates, db);

                return Ok(new { listInventoryResult });
            }
        }

        [HttpPost]
        [Route("GetSales")]
        public async Task<IActionResult> GetSales(string dates)
        {
            using (OledbCon db = new OledbCon())
            {
                await db.OpenAsync();

                var listOfSales = await _sales.ListSales(dates, db);

                return Ok(new { listOfSales });
            }
        }

        [HttpPost]
        [Route("Computation")]
        public async Task<IActionResult> GetComputation(string start)
        {
            var computation = await _ads.GetComputation(start);

            return Ok(computation);
        }

        [HttpPost]
        [Route("ImportClubs")]
        public async Task<IActionResult> ImportClubs()
        {
            using (OledbCon db = new OledbCon())
            {
                await db.OpenAsync();

                await _openQuery.ImportItems(db);

                return Ok();
            }
        }

        [HttpPost]
        [Route("ImportInventoryBackUp")]
        public async Task<IActionResult> ImportInventoryBackUp()
        {
            await _inventoryBackup.ManualImport();

            return Ok();
        }

        [HttpPost]
        [Route("ImportExcelFile")]
        public async Task<IActionResult> ImportExcelFile()
        {
            //string filePath = @"C:\Users\jbayoneta\Desktop\10-06-2023-Inventory.xlsx";
            string filePath = @"C:\Users\jbayoneta\Desktop\test.csv";
            var listClubs = new List<string> { "201", "203", "204", "205", "206", "207", "208", "209", "210", "211", "212", "213", "214", "215", "216", "217", "218", "219", "220", "221", "222", "223", "224", "225", "226", "227" };


            try
            {
                var excelData = new List<Dictionary<string, string>>();

                using (var spreadsheetDocument = SpreadsheetDocument.Open(filePath, false))
                {
                    var workbookPart = spreadsheetDocument.WorkbookPart;
                    var worksheetPart = workbookPart.WorksheetParts.First();
                    var worksheet = worksheetPart.Worksheet;
                    var sharedStringTablePart = workbookPart.SharedStringTablePart;

                    var rows = worksheet.Descendants<Row>().ToList();
                    var headerRow = rows.First();
                    var columnHeaders = headerRow.Elements<Cell>()
                        .Select(x => GetCellValue(x, sharedStringTablePart))
                        .ToList();

                    foreach (var row in rows.Skip(1)) // Skip the header row
                    {
                        var rowData = new Dictionary<string, string>();
                        var cells = row.Elements<Cell>().ToList();

                        for (int i = 0; i < cells.Count; i++)
                        {
                            string cellValue = GetCellValue(cells[i], sharedStringTablePart);
                            string columnHeader = columnHeaders[i];
                            rowData[columnHeader] = cellValue;
                        }

                        excelData.Add(rowData);
                    }

                    // Now, excelData contains the content of the Excel file.
                    // You can process the data or return it as needed. 

                    var inventories = new List<Inventory>();
                    var skuInClubs = new List<string>();
                    var skuInClubsDic = new Dictionary<string, Dictionary<string, string>>();

                    foreach (var club in listClubs)
                    {
                        using (OledbCon db = new OledbCon())
                        {
                            await db.Con.OpenAsync();
                            skuInClubs = await _openQuery.ListIventorySkuPerClub(db, club);
                            var test = skuInClubs.ToDictionary(x => x);
                            skuInClubsDic[club] = test;
                        }
                    }

                    foreach (var data in excelData)
                    {
                        data.TryGetValue("SKU", out var skuOut);

                        foreach (var club in listClubs)
                        {
                            data.TryGetValue($"{club}_INV_QTY", out var invOut);
                            skuInClubsDic.TryGetValue(club, out var test);


                            if (test.TryGetValue(skuOut, out var xxx))
                            {
                                var invInDecimal = Convert.ToDecimal(invOut);

                                var inventory = new Inventory()
                                {
                                    Sku = skuOut,
                                    Date = DateTime.Now.AddDays(-4),
                                    Clubs = club,
                                    Inv = invInDecimal > 0 ? invInDecimal : 0,
                                };

                                inventories.Add(inventory);
                            }
                            else
                            {
                                var inventory = new Inventory()
                                {
                                    Sku = skuOut,
                                    Date = DateTime.Now.AddDays(-4),
                                    Clubs = club,
                                    Inv = 0,
                                };

                                inventories.Add(inventory);
                            }
                        }
                    }

                    return Ok(inventories);
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error reading Excel file: {ex.Message}");
            }
        }

        private string GetCellValue(Cell cell, SharedStringTablePart sharedStringTablePart)
        {
            if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString)
            {
                if (int.TryParse(cell.InnerText, out int sharedStringIndex))
                {
                    var sharedStringItem = sharedStringTablePart.SharedStringTable.Elements<SharedStringItem>().ElementAt(sharedStringIndex);

                    return sharedStringItem.Text.Text;
                }
            }

            return cell.InnerText;
        }

        [HttpPost]
        [Route("ImportExcelFilePerDay")]
        public async Task<IActionResult> ImportExcelFilePerDay()
        {
            string filePath = @"C:\Users\jbayoneta\Desktop\10-07-2023-ADS.csv";
            var masterDictionary = new Dictionary<string, Dictionary<string, string>>();
            var listClubs = new List<string> { "201", "203", "204", "205", "206", "207", "208", "209", "210", "211", "212", "213", "214", "215", "216", "217", "218", "219", "220", "221", "222", "223", "224", "225", "226", "227" };

            var dateYesterday = DateTime.Now.AddDays(-6);
            var dateYWithZeroTime = new DateTime(dateYesterday.Year, dateYesterday.Month, dateYesterday.Day, 0, 0, 0, 0);

            var inventoryToday = await _invetory.GetInventoriesByDate(dateYWithZeroTime);
            var testInv = _invetory.GetDictionayOfPerClubhlInventory(inventoryToday);

            using (OledbCon db = new OledbCon())
            {


                foreach (var club in listClubs)
                {
                    await db.Con.OpenAsync();
                    var skuClubs = await _openQuery.ListIventorySkuPerClub(db, club);
                    var skuClubsDictionary = skuClubs.ToDictionary(x => x);
                    masterDictionary[club] = skuClubsDictionary;
                    db.Con.Close();
                }
            }

            try
            {
                var invList = new List<Inventory>();
                var items = await _item.GetAllItemSku();

                using (StreamReader reader = new StreamReader(filePath))
                {
                    var date = DateTime.Now.AddDays(-4);
                    var dateZeroTime = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, 0);

                    var count = 0;
                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();
                        string[] cells = line.Split(','); // Assuming CSV uses a comma as the delimiter
                        var sku = cells[2];
                        var club = cells[3];
                        var inv = cells[4].Contains("0E") ? "0" : cells[4];
                        var isInClub = false;

                        if (count != 0)
                        {

                            if (masterDictionary.TryGetValue(club, out var subDic))
                            {
                                isInClub = subDic.TryGetValue(sku, out var skuOut);
                            }
                        }

                        //var hasValue = testInv.TryGetValue($"{sku}{club}", out var inventoryOut);

                        if (count != 0 && listClubs.Contains(club) && isInClub )
                        {
                            var invModel = new Inventory()
                            {
                                Date = dateZeroTime,
                                Sku = sku,
                                Clubs = club,
                                Inv = Convert.ToDecimal(inv),
                            };

                            invList.Add(invModel);
                        }
                        count++;

                        Console.WriteLine();
                    }
                }

                var invDic = invList.GroupBy(x => new { x.Sku, x.Clubs }).ToDictionary(
                    x => x.Key,
                    y => new Inventory()
                    {
                        Inv = y.Sum(item => item.Inv),
                        Clubs = y.First().Clubs,
                        Sku = y.First().Sku,
                        Date = y.First().Date,
                    });

                var finalList = invDic.Values.ToList();

                //using (OledbCon db = new OledbCon())
                //{
                //    await db.Con.OpenAsync();

                //    using (var transaction = db.Con.BeginTransaction())
                //    {
                //        using (var bulkCopy = new SqlBulkCopy(db.Con, SqlBulkCopyOptions.Default, transaction))
                //        {
                //            bulkCopy.DestinationTableName = "tbl_inv";
                //            bulkCopy.BatchSize = 1000;

                //            var dataTable = new DataTable();
                //            dataTable.Columns.Add("Id", typeof(int));
                //            dataTable.Columns.Add("Date", typeof(DateTime));
                //            dataTable.Columns.Add("Sku", typeof(string));
                //            dataTable.Columns.Add("Inventory", typeof(decimal));
                //            dataTable.Columns.Add("Clubs", typeof(string));

                //            foreach (var rawData in finalList)
                //            {
                //                var row = dataTable.NewRow();
                //                row["Date"] = rawData.Date;
                //                row["Sku"] = rawData.Sku;
                //                row["Inventory"] = rawData.Inv;
                //                row["Clubs"] = rawData.Clubs;
                //                dataTable.Rows.Add(row);

                //            }
                //            await bulkCopy.WriteToServerAsync(dataTable);
                //        }

                //        transaction.Commit();
                //    }
                //}

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest($"Error reading Excel file: {ex.Message}");
            }
        }

    }
}
