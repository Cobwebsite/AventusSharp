using AventusSharp.Data;
using AventusSharpTest.Attribute;
using AventusSharpTest.Program.Data;
using AventusSharpTest.Program.Data.Abstract;
using AventusSharpTest.Tools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharpTest.Test.AAB_Create
{
    [TestFixture]
    [StopOnFail]
    public class Create
    {
        public static EuropeanCountry swiss;
        public static EuropeanCountry france;
        public static Location home;
        public static PersonHuman john;
        public static PersonHuman jane;
        public static PersonHuman junior;
        public static Cat felix;
        public static Cat filou;
        public static Dog medor;
        public static Dog snoopy;


        [Test]
        [Order(1)]
        public void SimpleCreate()
        {
            swiss = new()
            {
                PIB = 100,
                shortName = "CH"
            };
            NUnitExt.AssertNoError(swiss.CreateWithError());


        }

        [Test]
        [Order(2)]
        public void AutoCreate()
        {
            // TODO code it

            home = new() { name = "Home", country = swiss };
            NUnitExt.AssertNoError(home.CreateWithError());
        }

        [Test]
        [Order(3)]
        public void CreateWithLink()
        {
            john = new() { 
                firstname = "John", 
                lastname = "Doe", 
                location = home, 
                tags = new List<Tag>() { 
                    new Tag() { Name = "Man" },
                    new Tag() { Name = "Unknow" },
                } 
            };
            jane = new() { firstname = "Jane", lastname = "Doe", tags = new List<Tag>() { new Tag() { Name = "Woman" } } };
            junior = new() { firstname = "Junior", lastname = "Doe" };
            NUnitExt.AssertNoError(PersonHuman.CreateWithError(john));
            NUnitExt.AssertNoError(jane.CreateWithError());
            NUnitExt.AssertNoError(junior.CreateWithError());
        }

        [Test]
        [Order(4)]
        public void CreateInheritance()
        {
            felix = new()
            {
                name = "felix",
                color = "brun"
            };
            NUnitExt.AssertNoError(felix.CreateWithError());

            medor = new()
            {
                name = "medor"
            };
            NUnitExt.AssertNoError(Animal<IAnimal>.CreateWithError(medor));

            filou = new()
            {
                color = "white",
                name = "filou"
            };

            snoopy = new()
            {
                name = "snoopy"
            };

            NUnitExt.AssertNoError(Storable<IAnimal>.CreateWithError(new List<IAnimal>() { filou, snoopy }));
        }
    }
}
