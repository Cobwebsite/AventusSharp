using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Mysql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole.cs
{
    internal class StorageContainer
    {
        public static MySQLStorage storage1;
        public static MySQLStorage storage2;

        public static async Task Init()
        {
            await CreateStorage1();
            await CreateStorage2();
        }

        private static async Task CreateStorage1()
        {
            MySQLStorage storage = new(new StorageCredentials(
                host: "localhost",
                database: "aventus",
                username: "root",
                password: ""
            ));

            if (!await storage.Connect())
            {
                Console.WriteLine("Error during connection");
                throw new Exception();
            }
            await storage.ResetStorage();

            // storage.Debug = true;

            storage1 = storage;
        }

        private static async Task CreateStorage2()
        {
            MySQLStorage storage = new(new StorageCredentials(
                host: "localhost",
                database: "aventus2",
                username: "root",
                password: ""
            ));

            if (!await storage.Connect())
            {
                Console.WriteLine("Error during connection");
                throw new Exception();
            }
            await storage.ResetStorage();

            storage.Debug = true;

            storage2 = storage;
        }
    }
}
