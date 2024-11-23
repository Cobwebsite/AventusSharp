using AventusSharp.Data;
using AventusSharp.Tools;
using AventusSharpTest.Attribute;
using AventusSharpTest.Program.Data;
using AventusSharpTest.Program.Data.Abstract;
using AventusSharpTest.Test.AAB_Create;
using AventusSharpTest.Tools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharpTest.Test.AAC_Read
{
    [TestFixture]
    [StopOnFail]
    public class Where
    {
        [Test]
        public void SimpleWhere()
        {
            ResultWithError<List<IAnimal>> resultWithError = Storable<IAnimal>.WhereWithError(a => a.name == AAB_Create.Create.felix.name);
            NUnitExt.AssertNoError(resultWithError);

            Assert.IsTrue(resultWithError.Result.Count == 1);

            ResultWithError<List<Cat>> result2WithError = Cat.WhereWithError(c => c.color == AAB_Create.Create.felix.color);
            NUnitExt.AssertNoError(result2WithError);

            Assert.IsTrue(result2WithError.Result.Count == 1);
        }

        [Test]
        public void LinkWhere()
        {
            ResultWithError<List<PersonHuman>> resultWithError = PersonHuman.WhereWithError(p => p.location != null && p.location.name == Create.home.name);
            NUnitExt.AssertNoError(resultWithError);

            Assert.IsTrue(resultWithError.Result.Count == 1);
        }
    }
}
