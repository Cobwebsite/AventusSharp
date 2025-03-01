namespace AventusSharp.Routes
{
    public class RouterResolve
    {
        public RouteInfo RouteInfo { get; set; }

        public object?[] Params { get; set; }

        public RouterResolve(RouteInfo routeInfo, object?[] @params)
        {
            RouteInfo = routeInfo;
            Params = @params;
        }
    }
}
