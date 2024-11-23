using AventusSharp.Data;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Storage.Mysql;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

app.Use(async (context, next) =>
{
    await next();
});

var webSocketOptions = new WebSocketOptions()
{
    KeepAliveInterval = TimeSpan.FromSeconds(120),
};
app.UseWebSockets(webSocketOptions);

//app.UseHttpsRedirection();

//app.UseAuthorization();


AventusSharp.WebSocket.WebSocketMiddleware.Register();
AventusSharp.Routes.RouterMiddleware.Register();
app.Use(async (context, next) =>
{
    await AventusSharp.WebSocket.WebSocketMiddleware.OnRequest(context, next);
});
app.Use(async (context, next) =>
{
    await AventusSharp.Routes.RouterMiddleware.OnRequest(context, next);
});

app.MapControllers();

//MySQLStorage storage = new MySQLStorage(
//    new StorageCredentials(
//        host: "localhost",
//        database: "todo_api",
//        username: "maxime",
//        password: "pass$1234"
//    )
//    {
//        keepConnectionOpen = true,
//    });
//await DataMainManager.Register(new DataManagerConfig()
//{
//    defaultStorage = storage,
//    defaultDM = typeof(DatabaseDMSimple<>),
//});




app.Run();
