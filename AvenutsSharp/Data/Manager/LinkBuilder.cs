using System;

namespace AventusSharp.Data.Manager
{
    public class LinkBuilder
    {
        public Type FromType { get; set; }

        public string FieldName { get; set; }

        public int Id { get; set; }

        public LinkBuilder(Type fromType, string fieldName, int id)
        {
            FromType = fromType;
            FieldName = fieldName;
            Id = id;
        }
    }
}
