using BattleshipWebsocketServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5111");

builder.Services.AddSingleton<WebSocketService>();
builder.Services.AddSingleton<PlayersService>();
builder.Services.AddSingleton<RoomsService>();

var app = builder.Build();
app.UseWebSockets();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws" && context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var wsService = context.RequestServices.GetService<WebSocketService>();
        if (webSocket is not null && wsService is not null)
            await wsService.ProcessAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await next();
    }
});

app.Run();
