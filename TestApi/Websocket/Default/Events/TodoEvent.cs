using AventusSharp.WebSocket.Attributes;
using AventusSharp.WebSocket.Event;
using Path = AventusSharp.WebSocket.Attributes.Path;


namespace TestApi.Websocket.Default.Events
{
    [EndPoint<DefaultSocket>] // can be auto -> main
    [Path("/todo/myevent")] // can be auto -> namespace
    public class TodoEvent : WsEvent<TodoEvent.Body>
    {
        protected override async Task<Body> Prepare()
        {
            return new Body { id = 1, name = "" };
        }

        public class Body
        {
            public int id;
            public string name;
        }
    }
}
