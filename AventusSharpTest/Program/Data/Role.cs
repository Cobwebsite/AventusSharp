using AventusSharp.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace AventusSharpTest.Program.Data
{
    public class Role : Storable<PersonHuman>
    {
        public string name { get; set; }
    }
}
