using AventusSharp.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace AventusSharpTest.Program.Data.Abstract
{
    public interface IAnimal : IStorable, ITest
    {
        string name { get; set; }
    }
    public interface ITest
    {

    }
    public abstract class Animal<T> : Storable<T>, IAnimal, ITest where T : ITest, IAnimal
    {
        public string name { get; set; }
    }

    public interface IFelin: IAnimal { }
    public abstract class Felin<T> : Animal<T>, IFelin, ITest where T : IFelin, ITest
    {
        public string color { get; set; }
    }

    public class Cat : Felin<Cat>
    {
    }
    public class Dog : Animal<Dog>
    {

    }
}
