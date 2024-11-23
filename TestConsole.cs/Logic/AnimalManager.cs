//using AventusSharp.Data;
//using AventusSharp.Data.Manager;
//using AventusSharp.Data.Manager.DB;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Text;
//using System.Threading.Tasks;
//using TestConsole.cs.Data;
//using TestConsole.cs.Data.Abstract;

//namespace TestConsole.cs.Logic
//{
//    public class AnimalManager : GenericDatabaseDM<AnimalManager, IAnimal>
//    {
//        public override List<Type> DefineManualDependances()
//        {
//            return new List<Type>()
//            {
//                typeof(PersonHuman)
//            };
//        }

//        protected override bool? UseLocalCache()
//        {
//            return true;
//        }
//    }
//}
