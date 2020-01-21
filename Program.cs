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

namespace SqlImporter
{
    class FurniItem
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
                
            }

            this.Colour = data[7];
            this.Name = data[8];
            this.Description = data[9];
        }

        public FurniItem(int SpriteId)
        {
            this.SpriteId = SpriteId;
        }
    }

    class Program
    {
        private static List<FurniItem> itemList = new List<FurniItem>();

        static void Main(string[] args)
        {
            try
            {
                string fileContents = File.ReadAllText("furnidata.txt");
                var furnidataList = JsonConvert.DeserializeObject<List<string[]>>(fileContents);

                foreach (var stringArray in furnidataList)
                {
                    itemList.Add(new FurniItem(stringArray));
                }

                int nextCatalogueItemsId;
                int nextItemsDefinitionsId;
                int nextPageId;

                using (var connection = GetConnection())
                {
                    nextCatalogueItemsId = connection.QueryFirst<int>("SELECT AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_NAME = 'catalogue_items'");
                    nextItemsDefinitionsId = connection.QueryFirst<int>("SELECT AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_NAME = 'items_definitions'");
                    nextPageId = connection.QueryFirst<int>("SELECT AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_NAME = 'catalogue_pages'");

                    Console.WriteLine("Query success: " + nextItemsDefinitionsId + " / " + nextCatalogueItemsId);
                }

                FurniItem previousItem = null;
                StringBuilder sqlOutput = new StringBuilder();

                List<string> processedFurni = new List<string>();

                foreach (var file in Directory.GetFiles("ccts"))
                {
                    var fileName = Path.GetFileName(file);

                    if (fileName.StartsWith("hh_furni_xx_s_"))
                        fileName = fileName.Replace("hh_furni_xx_s_", "hh_furni_xx_");

                    var className = fileName;
                    
                    className = fileName.Replace("hh_furni_xx_", "");
                    className = Path.GetFileNameWithoutExtension(className);

                    if (processedFurni.Count(sprite => sprite == className) > 0)
                    {
                        continue;
                    }

                    var spriteData = RetrieveSpriteData(className, itemList);

                    if (spriteData == null)
                    {
                        Console.WriteLine("Next sprite ID: " + GetNextAvaliableSpriteId(previousItem != null ? previousItem.SpriteId : 1000));
                        Console.WriteLine("The furni " + className + " has no furnidata");
                    }
                    else
                    {
                        int defId = nextItemsDefinitionsId;
                        int catalogueItemsId = nextCatalogueItemsId;

                        sqlOutput.Append("INSERT INTO `items_definitions` (`id`, `sprite`, `name`, `description`, `sprite_id`, `length`, `width`, `top_height`, `max_status`, `behaviour`, `interactor`, `is_tradable`, `is_recyclable`, `drink_ids`, `rental_time`, `allowed_rotations`) VALUES " +
                            "(" + defId + ", '" + spriteData.FileName + "', '" + Escape(spriteData.Name) + "', '" + Escape(spriteData.Description) + "', " + spriteData.SpriteId + ", " + spriteData.Length + ", " + spriteData.Width + ", 0, '2', '" + (spriteData.Type == "i" ? "wall_item" : "solid") + "', 'default', 1, 1, '', -1, '0,2,4,6');");
                        
                        sqlOutput.Append("\n");
                        sqlOutput.Append("\n");

                        sqlOutput.Append("INSERT INTO `catalogue_items` (`id`, `sale_code`, `page_id`, `order_id`, `price_coins`, `price_pixels`, `hidden`, `amount`, `definition_id`, `item_specialspriteid`, `is_package`) " +
                            "VALUES (" + catalogueItemsId + ", '" + spriteData.FileName + "', '" + nextPageId + "', 2, 2, 0, 0, 1, " +defId + ", '', 0);");

                        sqlOutput.Append("\n");
                        sqlOutput.Append("\n");


                        nextItemsDefinitionsId++;
                        nextCatalogueItemsId++;

                        previousItem = spriteData;
                    }

                    processedFurni.Add(fileName);
                    File.WriteAllText("items.sql", sqlOutput.ToString());

                    //Console.WriteLine(Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.WriteLine("Done!");
            Console.Read();
        }

        private static string Escape(string name)
        {
            return name.Replace("'", "\\'");
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
                } else
                {
                    itemList.Add(new FurniItem(i));
                    return i;
                }
            }
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
