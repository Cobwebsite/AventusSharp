using AventusSharp.Data.Attributes;
using AventusSharp.Data;
using System.Collections.Generic;
using System;
using Nullable = AventusSharp.Data.Attributes.Nullable;

namespace TestConsole.cs.Data
{

    public class PersonHuman : Storable<PersonHuman>
    {
        public string firstname { get; set; }

        public string lastname { get; set; }

        public DateTime birthday { get; set; }

        [Nullable]
        public Location location { get; set; }

        [NotInDB]
        public Role role { get; set; }

        [AutoCRUD]
        public List<Tag> tags { get; set; } = new List<Tag>();
    }


}
