using AventusSharp.Routes;
using AventusSharp.Routes.Attributes;

namespace CSharpExport.TestFixture.Routes;

public abstract class GenericRouter<T> : Router
{
    [Post]
    public T Echo(T item) => item;
}
