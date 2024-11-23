using AventusSharp.Tools;
using AventusSharpTest.Attribute;
using AventusSharpTest.Program.Data;
using AventusSharpTest.Program.Data.Abstract;
using AventusSharpTest.Test.AAA_Init;
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
    public class GetAll
    {
        [Test]
        public void SimpleGetAll()
        {
            ResultWithError<List<PersonHuman>> peopleWithError = PersonHuman.GetAllWithError();
            NUnitExt.AssertNoError(peopleWithError);
            List<PersonHuman> people = peopleWithError.Result;
            Assert.IsTrue(people.Count == 2);
            Assert.IsTrue(people[0].Equals(AAB_Create.Create.john));
            Assert.IsTrue(people[1].Equals(AAB_Create.Create.jane));
            Assert.IsTrue(people[0].location == AAB_Create.Create.home);
        }

        [Test]
        public void InheritanceGetAll()
        {
            ResultWithError<List<IAnimal>> animalsWithError = Animal<IAnimal>.GetAllWithError();
            NUnitExt.AssertNoError(animalsWithError);

            List<IAnimal> animals = animalsWithError.Result;
            Assert.IsTrue(animals.Count == 4);
            Assert.IsTrue(animals[0].Equals(AAB_Create.Create.felix));
            Assert.IsTrue(animals[1].Equals(AAB_Create.Create.medor));
            Assert.IsTrue(animals[2].Equals(AAB_Create.Create.filou));
            Assert.IsTrue(animals[3].Equals(AAB_Create.Create.snoopy));
        }

        [Test]
        public void InheritanceChildGetAll()
        {
            ResultWithError<List<Dog>> dogsWithError = Dog.GetAllWithError();
            NUnitExt.AssertNoError(dogsWithError);

            List<Dog> dogs = dogsWithError.Result;
            Assert.IsTrue(dogs[0].Equals(AAB_Create.Create.medor));
            Assert.IsTrue(dogs[1].Equals(AAB_Create.Create.snoopy));

        }

        [Test]
        public void InheritanceChildAbstractGetAll()
        {
            ResultWithError<List<IFelin>> felinsWithError = Animal<IFelin>.GetAllWithError();
            NUnitExt.AssertNoError(felinsWithError);

            List<IFelin> felins = felinsWithError.Result;
            Assert.IsTrue(felins[0].Equals(AAB_Create.Create.felix));
            Assert.IsTrue(felins[1].Equals(AAB_Create.Create.filou));

        }
    }
}
