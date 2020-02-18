using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Dapper;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlImporter.Products;

namespace SqlImporter
{
    public class FurniItem
    {
        public string Type;
        public int SpriteId;
        public string FileName;
        public string Revision;
        public string Unknown;
        public int Length;
        public int Width;
        public string Colour;
        public string Name;
        public string Description;
        public string[] RawData
        {
            get
            {
                return new string[] { Type, Convert.ToString(SpriteId), FileName, Revision, Unknown, Length == -1 ? "" : Convert.ToString(Length), Width == -1 ? "" : Convert.ToString(Width), Colour, Name, Description };
            }
        }

        public bool Ignore;

        public FurniItem(string[] data)
        {
            this.Type = data[0];
            this.SpriteId = int.Parse(data[1]);
            this.FileName = data[2];
            this.Revision = data[3];
            this.Unknown = data[4];
            try
            {
                this.Length = Convert.ToInt32(data[5]);
                this.Width = Convert.ToInt32(data[6]);
            }
            catch (Exception ex)
            {
                this.Length = -1;
                this.Width = -1;
            }

            this.Colour = data[7];
            this.Name = data[8];
            this.Description = data[9];
        }

        public FurniItem(int SpriteId)
        {
            this.SpriteId = SpriteId;
            this.Ignore = true;
        }
    }

    class Program
    {
        private static StringBuilder sqlOutput = new StringBuilder();
        private static List<FurniItem> itemList = new List<FurniItem>();
        private static List<FurniItem> officialItemList = new List<FurniItem>();

        private static int nextCatalogueItemsId;
        private static int nextItemsDefinitionsId;

        public static StringBuilder SQLBuilder
        {
            get { return sqlOutput; }
        }

        public static int NextCatalogueId
        {
            get { return nextCatalogueItemsId; }
            set { nextCatalogueItemsId = value; }
        }

        public static int NextDefinitionId
        {
            get { return nextItemsDefinitionsId; }
            set { nextItemsDefinitionsId = value; }
        }

        internal static string OUTPUT_DIR = "data/";
        internal static int PageId;

        static void Main(string[] args)
        {
            try
            {
                var fileContents = File.ReadAllText(OUTPUT_DIR + "old_data/furnidata.txt");
                var furnidataList = JsonConvert.DeserializeObject<List<string[]>>(fileContents);

                foreach (var stringArray in furnidataList)
                {
                    itemList.Add(new FurniItem(stringArray));
                }

                var officialFileContents = File.ReadAllText(OUTPUT_DIR + "official_furnidata.txt");
                officialFileContents = officialFileContents.Replace("]]\r\n[[", "],[");
                var officialFurnidataList = JsonConvert.DeserializeObject<List<string[]>>(officialFileContents);

                foreach (var stringArray in officialFurnidataList)
                {
                    officialItemList.Add(new FurniItem(stringArray));
                }

                using (var connection = GetConnection())
                {
                    nextCatalogueItemsId = connection.QueryFirst<int>("SELECT AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_NAME = 'catalogue_items'");
                    nextItemsDefinitionsId = connection.QueryFirst<int>("SELECT AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_NAME = 'items_definitions'");
                    PageId = connection.QueryFirst<int>("SELECT AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_NAME = 'catalogue_pages'") - 1;

                    Console.WriteLine("Query success: " + nextItemsDefinitionsId + " / " + nextCatalogueItemsId);
                }

                FurniItem previousItem = null;

                List<string> badApples = new List<string>();
                List<string> processQueue = new List<string>();

                List<FurniItem> processedFurni = new List<FurniItem>();

                foreach (var file in Directory.GetFiles("ccts"))
                {
                    var fileName = Path.GetFileName(file);

                    if (fileName.StartsWith("hh_furni_xx_s_"))
                        fileName = fileName.Replace("hh_furni_xx_s_", "hh_furni_xx_");

                    if (fileName.StartsWith("o_"))
                        fileName = fileName.Replace("o_", "hh_furni_xx_");

                    var className = fileName;

                    className = fileName.Replace("hh_furni_xx_", "");
                    className = Path.GetFileNameWithoutExtension(className);

                    if (className.Contains("#"))
                    {
                        string contents = className.Substring(className.IndexOf("#") + 1);

                        if (contents.Contains("-"))
                        {
                            string[] data = contents.Split('-');

                            int minimumRange = int.Parse(data[0]);
                            int maximumRange = int.Parse(data[1]);

                            for (int i = minimumRange; i <= maximumRange; i++)
                            {
                                var newClass = className.Replace("#" + minimumRange + "-" + maximumRange, "*" + i);

                                if (!processQueue.Contains(newClass)) 
                                    processQueue.Add(newClass);

                            }
                        }
                        else
                        {
                            className = className.Replace("#", "*");
  
                            if (!processQueue.Contains(className))
                                processQueue.Add(className);
                        }
                    }
                    else
                    {
                        if (!processQueue.Contains(className))
                            processQueue.Add(className);
                    }
                    //Console.WriteLine(Path.GetFileName(file));
                }

                foreach (var className in processQueue)
                {
                    if (processedFurni.Count(sprite => sprite.FileName == className) > 0)
                    {
                        continue;
                    }

                    var spriteData = RetrieveSpriteData(className, itemList);

                    if (spriteData == null)
                    {
                        string newFurniName;
                        spriteData = RetryFindSprite(className, out newFurniName);

                        //spriteData = RetrieveSpriteData(newFurniName, officialItemList);

                        if (spriteData != null)
                        {
                            if (className != newFurniName)
                            {
                                var newFurni = new FurniItem(spriteData.RawData);
                                newFurni.FileName = className;
                                newFurni.SpriteId = GetNextAvaliableSpriteId(spriteData.SpriteId);

                                if (itemList.Count(item => item.SpriteId == spriteData.SpriteId) > 0)
                                {
                                    newFurni.SpriteId = GetNextAvaliableSpriteId(spriteData.SpriteId);
                                }

                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine("The furniture \"" + className + "\" was missing but has now been added");
                                Console.ResetColor();

                                spriteData = newFurni;
                                itemList.Add(newFurni);
                            }
                            else
                            {
                                var newFurni = new FurniItem(spriteData.RawData);

                                if (itemList.Count(item => item.SpriteId == spriteData.SpriteId) > 0)
                                {
                                    newFurni.SpriteId = GetNextAvaliableSpriteId(spriteData.SpriteId);
                                }

                                Console.ForegroundColor = ConsoleColor.DarkYellow;
                                Console.WriteLine("The furniture \"" + className + "\" was missing but added and sprite id has been recalulated");
                                Console.ResetColor();

                                spriteData = newFurni;
                                itemList.Add(newFurni);
                            }
                        }
                    }

                    if (spriteData != null)
                    {
                        /*if (spriteData == null)
                        {
                            Console.WriteLine("Next sprite ID: " + GetNextAvaliableSpriteId(previousItem != null ? previousItem.SpriteId : 1000));
                            Console.WriteLine("The furni " + className + " has no furnidata");
                        }
                        else
                        {*/
                        if (!HasItemEntry(className))
                        {
                            /*   Console.WriteLine("The entry for " + className + " already exists in the database");
                           }
                           else
                           {*/
                            int defId = nextItemsDefinitionsId;
                            int catalogueItemsId = nextCatalogueItemsId;

                            sqlOutput.Append("INSERT INTO `items_definitions` (`id`, `sprite`, `name`, `description`, `sprite_id`, `length`, `width`, `top_height`, `max_status`, `behaviour`, `interactor`, `is_tradable`, `is_recyclable`, `drink_ids`, `rental_time`, `allowed_rotations`) VALUES " +
                                "(" + defId + ", '" + spriteData.FileName + "', '" + Escape(spriteData.Name) + "', '" + Escape(spriteData.Description) + "', " + spriteData.SpriteId + ", " + spriteData.Length + ", " + spriteData.Width + ", 0, '2', '" + (spriteData.Type == "i" ? "wall_item" : "solid") + "', 'default', 1, 1, '', -1, '0,2,4,6');");

                            sqlOutput.Append("\n");
                            sqlOutput.Append("\n");

                            AddCatalogueItem(null, spriteData, PageId, nextItemsDefinitionsId);
                            nextItemsDefinitionsId++;

                            previousItem = spriteData;
                        }

                        processedFurni.Add(spriteData);
                        //}
                    }
                    else
                    {
                        badApples.Add(className);
                    }
                }

                ProductData.AddItems(processedFurni);

                if (badApples.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("The following were bad apples:");

                    foreach (string className in badApples)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine(className);
                    }
                    
                    Console.ResetColor();
                }

                ProductData.HandleDeals();
                ProductData.WriteProducts(Program.OUTPUT_DIR + "productdata.txt");

                List<string> processedOrderQueue = new List<string>();
                var sortedItems = ProductData.Items.Where(item => processQueue.Contains(item.SaleCode) && item.SaleCode.Length > 0).ToList().ToList();

                int orderId = 0;


                Console.WriteLine("Order ID start (blank for 0):");
                var orderIdValue = Console.ReadLine();

                if (orderIdValue.Length > 0)
                {
                    orderId = int.Parse(orderIdValue);
                }

                foreach (var item in sortedItems)
                {
                    if (processedOrderQueue.Count(i => i == item.SaleCode) == 0)
                    {
                        sqlOutput.Append("UPDATE catalogue_items SET order_id = '" + orderId++ + "' WHERE sale_code = '" + item.SaleCode + "';");
                        sqlOutput.Append("\n");
                        sqlOutput.Append("\n");
                        processedOrderQueue.Add(item.SaleCode);
                    }
                }

                File.WriteAllText(OUTPUT_DIR + "items.sql", sqlOutput.ToString());
                RebuildFurnidata(itemList, OUTPUT_DIR + "furnidata.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.WriteLine("Done!");
            Console.Read();
        }

        private static FurniItem RetryFindSprite(string className, out string newFurniName)
        {
            FurniItem sprite = null;

            List<string> suffixes = new List<string>();
            suffixes.Add("cmp");
            suffixes.Add("_cmp");
            suffixes.Add("camp");
            suffixes.Add("_camp");
            suffixes.Add("campaign");
            suffixes.Add("_campaign");
            suffixes.Add("c");
            suffixes.Add("_c");

            foreach (var suffix in suffixes)
            {
                if (!className.EndsWith(suffix))
                    continue;

                newFurniName = className.Substring(0, className.Length - suffix.Length);
                sprite = RetrieveSpriteData(newFurniName, itemList);

                if (sprite != null)
                {
                    return sprite;
                }

                sprite = RetrieveSpriteData(newFurniName, officialItemList);

                if (sprite != null)
                {
                    return sprite;
                }
            }

            newFurniName = className;
            sprite = RetrieveSpriteData(newFurniName, officialItemList);

            if (sprite != null)
            {
                return sprite;
            }

            return null;
        }

        private static FurniItem RetrieveSpriteData(string className, List<FurniItem> itemList)
        {
            foreach (var item in itemList)
            {
                if (item.FileName == null)
                {
                    continue;
                }

                if (item.FileName.Equals(className, StringComparison.InvariantCultureIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static int GetNextAvaliableSpriteId(int startId)
        {
            int i = startId;

            while (true)
            {
                if (itemList.Count(item => item.SpriteId == i) > 0)
                {
                    i++;
                }
                else
                {
                    itemList.Add(new FurniItem(i));
                    return i;
                }
            }
        }

        public static void AddCatalogueItem(string saleCode, FurniItem spriteData, int nextPageId, int definitionId)
        {
            sqlOutput.Append("INSERT INTO `catalogue_items` (`id`, `sale_code`, `page_id`, `order_id`, `price_coins`, `price_pixels`, `hidden`, `amount`, `definition_id`, `item_specialspriteid`, `is_package`) " +
            "VALUES (" + NextCatalogueId + ", '" + (saleCode == null ? spriteData.FileName : saleCode) + "', '" + nextPageId + "', 2, 2, 0, 0, 1, " + definitionId + ", '', 0);");

            sqlOutput.Append("\n");
            sqlOutput.Append("\n"); 
            
            nextCatalogueItemsId++;
        }

        public static bool HasItemEntry(string className)
        {
            using (var connection = GetConnection())
            {
                var queryParameters = new DynamicParameters();
                queryParameters.Add("@sprite", className);

                return connection.QueryFirstOrDefault<string>("SELECT behaviour FROM items_definitions WHERE sprite = @sprite", queryParameters) != null;
            }
        }

        public static int GetItemEntryId(string className)
        {
            using (var connection = GetConnection())
            {
                var queryParameters = new DynamicParameters();
                queryParameters.Add("@sprite", className);

                return connection.QueryFirstOrDefault<int>("SELECT id FROM items_definitions WHERE sprite = @sprite", queryParameters);
            }
        }

        private static void RebuildFurnidata(List<FurniItem> itemList, string fileName)
        {
            List<string> furniEntry = new List<string>();

            foreach (var item in itemList)
            {
                if (item.Ignore)
                    continue;

                string fileOutput = "";

                fileOutput += "[\"";
                fileOutput += string.Join("\",\"", item.RawData.Take<string>(10));
                fileOutput += "\"]";

                furniEntry.Add(fileOutput);
            }

            File.WriteAllText(fileName, "[" + string.Join(",", furniEntry) + "]");
        }

        private static string Escape(string name)
        {
            return name.Replace("'", "\\'");
        }

        public static IDbConnection GetConnection()
        {
            MySqlConnectionStringBuilder connectionString = new MySqlConnectionStringBuilder();
            connectionString.Server = "localhost";
            connectionString.UserID = "root";
            connectionString.Password = "123";
            connectionString.Database = "dev";
            connectionString.Port = 3306;
            connectionString.MinimumPoolSize = 0;
            connectionString.MaximumPoolSize = 30;

            var dbConnection = new MySqlConnection(connectionString.ToString());
            dbConnection.Open();

            return dbConnection;
        }
    }
}
