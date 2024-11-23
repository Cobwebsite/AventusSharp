using AventusSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestConsole.cs.Data
{
    public class Tag : Storable<Tag>
    {
        public string Name { get; set; }
    }
}
