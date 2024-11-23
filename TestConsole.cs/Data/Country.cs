using AventusSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole.cs.Data
{
    public interface ICountry : IStorable
    {
        public string shortName { get; set; }
    }
    public abstract class Country<T> : Storable<T>, ICountry where T : ICountry
    {
        public string shortName { get; set; }
    }

    public class EuropeanCountry : Country<EuropeanCountry>
    {
        public int PIB { get; set; }
    }
}
