using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Response
{
    public class DummyResponse : TextResponse
    {
        public DummyResponse() : base("Im dummy")
        {
        }
    }
}
