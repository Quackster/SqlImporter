using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SqlImporter.Products
{
    public class ProductDataEntry
    {
        public string SaleCode;
        public string Name;
        public string Description;
        public string GraphicsData;

        public ProductDataEntry(string saleCode, string name, string description, string graphicsData)
        {
            SaleCode = Strip(saleCode);
            Name = Strip(name).Replace("\"&QUOTE&\"", "\"");
            Description = Strip(description).Replace("\"&QUOTE&\"", "\"");
            GraphicsData = Strip(graphicsData);
        }

        public static string Strip(string value)
        {
            return value.Length > 1 ? value.Substring(1, value.Length - 2) : string.Empty;
        }

        public string[] Output
        {
            get
            {
                return new string[] { SaleCode, Name.Replace("\"", "\"&QUOTE&\""), Description.Replace("\"", "\"&QUOTE&\""), GraphicsData };
            }
        }

        public override string ToString()
        {
            return string.Join(", ", Output);
        }

    }

    public class ProductData
    {
        private static List<FurniItem> processedItems = new List<FurniItem>();
        private static List<ProductDataEntry> productList = new List<ProductDataEntry>();

        internal static List<ProductDataEntry> Items
        {
            get { return productList; }
        }

        public static void AddItems(List<FurniItem> items)
        {
            processedItems = items;

            ReadProducts(Program.OUTPUT_DIR + "old_data/productdata.txt");

            foreach (var item in items)
            {
                /*if (productList.Count(product => product.SaleCode == item.FileName) > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Product \"" + item.FileName + "\" already exists!");
                    Console.ResetColor();
                }
                else*/
                if (!(productList.Count(product => product.SaleCode == item.FileName) > 0))
                {

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Added \"" + item.FileName + "\" to new productdata!");
                    Console.ResetColor();
                    
                    productList.Add(new ProductDataEntry("\"" + item.FileName + "\"", "\"" + item.Name + "\"", "\"" + item.Description + "\"", ""));
                }

                
            }
        }

        public static void HandleDeals()
        {
            while (true)
            {
                Console.WriteLine("Add deal? Y/N");
                var createDeal = Console.ReadLine();

                if (createDeal.ToUpper() == "Y")
                {
                    HandleDeal();
                }
                else
                {
                    break;
                }
            }
        }

        private static void HandleDeal()
        {
            Console.WriteLine("Product data sale code:");
            var saleCode = Console.ReadLine();

            if (productList.Count(product => product.SaleCode == saleCode) > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Product \"" + saleCode + "\" already exists!");
                Console.ResetColor();
                return;
            }

            Console.WriteLine("Product data name:");
            var name = Console.ReadLine();

            Console.WriteLine("Product data description:");
            var description = Console.ReadLine();

            Program.AddCatalogueItem(saleCode, null, Program.PageId, 0);

            while (true)
            {
                Console.WriteLine("Deal sprite name:");
                var spriteName = Console.ReadLine();

                if (!Program.HasItemEntry(spriteName))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Product \"" + spriteName + "\" doesn't exist!");
                    Console.ResetColor();
                    continue;
                }

                var definitionId = Program.GetItemEntryId(spriteName);

                Console.WriteLine("Deal amount:");
                var spriteAmount = int.Parse(Console.ReadLine());

                Console.WriteLine("Add another item? Y/N");
                var nextItem = Console.ReadLine();

                Program.SQLBuilder.Append("INSERT INTO `catalogue_packages` (`salecode`, `definition_id`, `special_sprite_id`, `amount`) VALUES ('" + saleCode + "', '" + definitionId + "', 0, '" + spriteAmount + "');");
                Program.SQLBuilder.Append("\n");
                Program.SQLBuilder.Append("\n");

                if (nextItem.ToUpper() == "N")
                {
                    break;
                }
            }

            productList.Add(new ProductDataEntry(saleCode, name, description, ""));
        }

        public static void WriteProducts(string outputFile)
        {
            List<string> productDataList = new List<string>();

            foreach (var product in productList)
            {
                string fileOutput = "";

                fileOutput += "[\"";
                fileOutput += string.Join("\",\"", product.Output);
                fileOutput += "\"]";

                productDataList.Add(fileOutput);
            }

            var finalOutput = "";
            var chunks = SplitList(productDataList);

            foreach (var chunk in chunks)
            {
                finalOutput += "[";
                finalOutput += string.Join(",", chunk);
                finalOutput += "]";
                finalOutput += "\r\n";
            }

            File.WriteAllText(outputFile, finalOutput);
        }

        private static void ReadProducts(string fileName)
        {
            string fileContents = File.ReadAllText(fileName);
            fileContents = fileContents.Replace("]]\r\n[[", "],[");
            fileContents = fileContents.Replace("]]", "]");
            fileContents = fileContents.Replace("[[", "[");
            fileContents = fileContents.Replace("],[", "]|[");
            //fileContents = fileContents.Replace("\"&QUOTE&\"", "\"");

            string[] chunks = Regex.Split(fileContents, "\n\r{1,}|\n{1,}|\r{1,}", RegexOptions.Multiline);
            foreach (string chunk in chunks)
            {
                MatchCollection collection = Regex.Matches(chunk, @"\[+?((.)*?)\]");
                foreach (Match item in collection)
                {

                    string itemData = item.Value;
                    List<string> splitted = new List<string>();

                    itemData = itemData.Substring(1, itemData.Length - 2);
                    itemData = itemData.Replace("\",\"", "\"|\"");

                    string[] matches = itemData.Split('|');


                    var entry = new ProductDataEntry(
                        matches[0],
                        matches[1],
                        matches[2],
                        matches[3]);

                    productList.Add(entry);


                }
            }

            Console.WriteLine("Read " + productList.Count + " products!");
        }

        public static IEnumerable<List<T>> SplitList<T>(List<T> locations, int nSize = 101)
        {
            for (int i = 0; i < locations.Count; i += nSize)
            {
                yield return locations.GetRange(i, Math.Min(nSize, locations.Count - i));
            }
        }
    }
}
