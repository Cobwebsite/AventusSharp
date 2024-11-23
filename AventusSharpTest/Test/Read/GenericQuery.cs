using AventusSharpTest.Attribute;
using AventusSharpTest.Program.Data;
using AventusSharpTest.Tools;
using AventusSharpTest.Test.AAB_Create;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AventusSharp.Tools;

namespace AventusSharpTest.Test.AAC_Read
{
    [TestFixture]
    [StopOnFail]
    public class GenericQuery
    {

        [Test]
        public void SimpleGenericQuery()
        {
            string firstname = Create.john.firstname;
            ResultWithError<List<PersonHuman>> resultWithError = PersonHuman.StartQuery().Where(p => p.firstname == firstname).RunWithError();
            NUnitExt.AssertNoError(resultWithError);

            Assert.IsTrue(resultWithError.Result.Count == 1);
            Assert.IsTrue(resultWithError.Result[0].Id == Create.john.Id);
        }
    }

}
