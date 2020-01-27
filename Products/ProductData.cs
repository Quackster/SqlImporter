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
            SaleCode = ProductData.Strip(saleCode);
            Name = ProductData.Strip(name);
            Description = ProductData.Strip(description);
            GraphicsData = ProductData.Strip(graphicsData);
        }

        public string[] Output
        {
            get
            {
                return new string[] { SaleCode, Name, Description.Replace("&QUOTE&", "\"&QUOTE&\""), GraphicsData };
            }
        }

        public override string ToString()
        {
            return string.Join(", ", Output);
        }

    }

    public class ProductData
    {
        private static List<ProductDataEntry> productList = new List<ProductDataEntry>();

        public static void AddItems(List<FurniItem> items)
        {
            ReadProducts(Program.OUTPUT_DIR + "old_productdata.txt");

            foreach (var item in items)
            {
                if (productList.Count(product => product.SaleCode == item.FileName) > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Product " + item.FileName + " already exists!");
                    Console.ResetColor();
                    continue;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Added " + item.FileName + " to new productdata!");
                Console.ResetColor();

                productList.Add(new ProductDataEntry(item.FileName, item.Name, item.Description, ""));
            }

            WriteProducts(Program.OUTPUT_DIR + "productdata.txt", productList);
        }

        private static void WriteProducts(string outputFile, List<ProductDataEntry> productList)
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
            fileContents = fileContents.Replace("\"&QUOTE&\"", "&QUOTE&");

            foreach (string productRaw in fileContents.Split('|'))
            {
                if (productRaw.Count(f => f == '"') > 8)
                {
                    Console.WriteLine("Warning! The following item has more than 8 double quotes, use \"&QUOTE&\" when using quotes.");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(productRaw);
                    Console.ResetColor();
                    continue;
                }

                var newProduct = productRaw;
                newProduct = newProduct.TrimStart('[');
                newProduct = newProduct.TrimEnd(']');

                var reg = new Regex("\".*?\"");
                var matches = reg.Matches(newProduct);

                var entry = new ProductDataEntry(
                    matches[0].Value,
                    matches[1].Value,
                    matches[2].Value,
                    matches[3].Value);

                productList.Add(entry);

                
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

        public static string Strip(string value)
        {
            return value.Replace("\"", "");
        }
    }
}
