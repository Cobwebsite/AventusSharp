using AventusSharp;
using AventusSharp.Attributes;
using AventusSharp.Data;
using AventusSharp.Data.Attributes;


namespace AventusSharpTest.Program.Data
{
    public class Location : Storable<Location>
    {
        public string name { get; set; }

        [AutoCreate, AutoUpdate, AutoDelete]
        public ICountry country { get; set; }
    }
}
