using AventusSharp.Data;

namespace TestApi.Data
{
    public class Todo : Storable<Todo>
    {
        [AventusSharp.Data.Attributes.Nullable]
        public string name { get; set; }

        public string description { get; set; }
    }
}
