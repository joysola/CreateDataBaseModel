using DatabaseSchemaReader;
using Humanizer;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace TestCreateMySqlModel
{
    /// <summary>
    /// 
    /// </summary>
    class Program
    {
        //private static string _schemaOwnerName = "dst3.0";
        //private static string _schemaOwnerName2 = "dst_local";

        //private static string _connectionStr = "Server=192.168.1.24;Port=31101;Uid=root;Pwd=root;Database=dst3.0;SslMode=None;";
        //private static string _connectionStr2 = "Server=192.168.101.65;Port=3306;Uid=root;Pwd=root;Database=dst_local;SslMode=None;";

        private static string _tableClass = "TableClass.txt";
        private static string _property = "Property.txt";
        private static string _modelDir = $"{Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)}\\Models\\";


        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var connectionStr = string.Empty;
            var schemaOwnerName = string.Empty;
            var tableInfo = string.Empty;
            var propertyInfo = string.Empty;
            try
            {
                IConfiguration configuration = new ConfigurationBuilder()
                   .AddJsonFile("appsettings.json", true, true)
                   //.AddUserSecrets<Program>()
                   .Build();
                connectionStr = configuration.GetConnectionString("MySqlConnectionStr");
                schemaOwnerName = configuration.GetValue<string>("Setting:SchemaOwnerName");
                tableInfo = File.ReadAllTextAsync(_tableClass).Result;
                propertyInfo = File.ReadAllTextAsync(_property).Result;

            }
            catch
            {
                Console.WriteLine("读取配置错误！");
                Console.Read();
                return;
            }

            if (string.IsNullOrEmpty(connectionStr) || string.IsNullOrEmpty(schemaOwnerName) || string.IsNullOrEmpty(tableInfo) || string.IsNullOrEmpty(propertyInfo))
            {
                Console.WriteLine("配置信息为空！");
                Console.Read();
                return;
            }

            // 读取配置成功后
            using (var connection = new MySqlConnection(connectionStr))
            {
                var dbReader = new DatabaseReader(connection);
                //Then load the schema (this will take a little time on moderate to large database structures)
                var schema = dbReader.ReadAll();
                var dst30Tables = schema.Tables.Where(x => x.SchemaOwner == schemaOwnerName).ToList();
                //The structure is identical for all providers (and the full framework).
                foreach (var table in dst30Tables)
                {
                    var columnInfoList = new StringBuilder();
                    //do something with your model
                    foreach (var column in table.Columns)
                    {
                        var dataType = column.DataType.NetDataTypeCSharpName; // .net类型
                        var defaultValue = string.Empty;
                        // 1.主键标记
                        var attriInfo = column.IsPrimaryKey ? "[Key]" : "";
                        // 2.非空标记
                        if (!column.IsPrimaryKey && !column.Nullable)
                        {
                            attriInfo += "[Required]";
                        }
                        // 3.可空类型需要增加可空符号
                        if (column.Nullable
                            && column.DataType.IsDateTime || column.DataType.IsNumeric)
                        {
                            dataType += "?";
                        }
                        // 4.默认值修改
                        if (!string.IsNullOrEmpty(column.DefaultValue))
                        {
                            defaultValue = $" = {column.DefaultValue};";
                            if (column.DataType.NetDataTypeCSharpName == "decimal")
                            {
                                defaultValue = $" = {column.DefaultValue}m;";
                            }
                            if (column.DataType.IsString)
                            {
                                defaultValue = $" = \"{ column.DefaultValue}\";";
                            }
                        }
                        var newColumnName = column.Name.Humanize(LetterCasing.Title).Replace(" ", "_"); // 首字母大写
                        var columnInfo = string.Format(propertyInfo, dataType, newColumnName /*column.Name*/, column.Description, attriInfo, defaultValue);
                        columnInfoList.AppendLine(columnInfo);
                    }
                    var newTableName = table.Name.Humanize(LetterCasing.Title).Replace(" ", "_"); // 首字母大写
                    var tableinfo = string.Format(tableInfo, "DST.Database.Model", newTableName/*table.Name*/, newTableName/*table.Name*/, columnInfoList.ToString(), table.Description);
                    if (!Directory.Exists(_modelDir))
                    {
                        Directory.CreateDirectory(_modelDir);
                    }
                    using (var fs = new FileStream($"{_modelDir}{newTableName/*table.Name*/}.cs", FileMode.Create, FileAccess.ReadWrite))
                    {
                        fs.Write(Encoding.Default.GetBytes(tableinfo));
                    }
                }
            }

            Console.WriteLine("Successed！");
            Console.ReadKey();
        }
    }
}
