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

namespace AventusSharpTest.Test.AAE_Delete
{

    [TestFixture]
    [StopOnFail]
    public class Delete
    {

        [Test]
        [Order(1)]
        public void DeleteLinkFailed()
        {
            List<GenericError> errors = Create.home.DeleteWithError();
            NUnitExt.AssertError(errors);
        }
        [Test]
        [Order(2)]
        public void SimpleDelete()
        {
            NUnitExt.AssertNoError(PersonHuman.DeleteWithError(Create.john));
            NUnitExt.AssertNoError(Create.jane.DeleteWithError());

            NUnitExt.AssertNoError(Create.felix.DeleteWithError());
            NUnitExt.AssertNoError(Animal<IAnimal>.DeleteWithError(Create.medor));

            NUnitExt.AssertNoError(Storable<IAnimal>.DeleteWithError(new List<IAnimal>() { Create.filou, Create.snoopy }));
        }

        [Test]
        [Order(3)]
        public void DeleteLink()
        {
            List<GenericError> errors = Create.home.DeleteWithError();
            NUnitExt.AssertNoError(errors);
        }
    }
}
