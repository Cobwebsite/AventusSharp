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

        public static void Init()
        {
            CreateStorage1();
            CreateStorage2();
        }

        private static void CreateStorage1()
        {
            MySQLStorage storage = new(new StorageCredentials(
                host: "localhost",
                database: "aventus",
                username: "root",
                password: ""
            ));

            if (!storage.Connect())
            {
                Console.WriteLine("Error during connection");
                throw new Exception();
            }
            storage.ResetStorage();

            storage.Debug = true;

            storage1 = storage;
        }

        private static void CreateStorage2()
        {
            MySQLStorage storage = new(new StorageCredentials(
                host: "localhost",
                database: "aventus2",
                username: "root",
                password: ""
            ));

            if (!storage.Connect())
            {
                Console.WriteLine("Error during connection");
                throw new Exception();
            }
            storage.ResetStorage();

            storage.Debug = true;

            storage2 = storage;
        }
    }
}
