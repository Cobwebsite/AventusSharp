using AventusSharp.Data;
using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Mysql;
using AventusSharp.Tools;
using AventusSharpTest.Attribute;
using AventusSharpTest.Program.Data.Abstract;
using AventusSharpTest.Tools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharpTest.Test.AAA_Init
{
    [TestFixture]
    [StopOnFail]
    public class Init
    {
        public static MySQLStorage storage;

        [Test]
        [Order(1)]
        public void TestConnection()
        {
            storage = new(new StorageCredentials(
                host: "localhost",
                database: "aventus",
                username: "root",
                password: ""
            ));
            NUnitExt.AssertNoError(storage.ConnectWithError());
        }

        [Test]
        [Order(2)]
        public void ResetStorage()
        {
            NUnitExt.AssertNoError(storage.ResetStorage());
        }

        [Test]
        [Order(3)]
        public void InitManager()
        {
            DataMainManager.Configure(config =>
            {
                config.defaultStorage = storage;
                config.defaultDM = typeof(SimpleDatabaseDM<>);
                config.preferLocalCache = true;
                config.preferShortLink = false;
                config.nullByDefault = false;
            });
            Task<VoidWithError> resultTemp = DataMainManager.Init(Assembly.GetExecutingAssembly());
            resultTemp.Wait();
            NUnitExt.AssertNoError(resultTemp.Result);
            GenericDM.Get(typeof(IAnimal));
        }
    }
}
