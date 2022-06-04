namespace BattleshipWebsocketServer.Tests;

using BattleshipWebsocketServer.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

internal static class Util
{
    private static WebApplicationFactory<Program> _app;
    private static WebSocketClient? _webSocketClient;

    static Util()
    {
        ResetApp();
    }

    internal static void ResetApp()
    {
        _app = new WebApplicationFactory<Program>();
        _webSocketClient = _app.Server.CreateWebSocketClient();
    }

    internal static async Task<WebSocket> LogIn(string nickname = "Util")
    {
        var message = CreateMessage(WsMessage.MessageType.Request, "login", new { nickname = nickname });
        var ws = await SendMessage(message);
        await Receive(ws);
        return ws;
    }

    internal static async Task LogIn(WebSocket ws, string nickname = "Util")
    {
        var message = CreateMessage(WsMessage.MessageType.Request, "login", new { nickname = nickname });
        await SendMessage(ws, message);
        await Receive(ws);
    }

    internal static async Task<WebSocket> SendMessage(string message, bool ignoreWelcome = true)
    {
        var ws = await GetWebSocket(ignoreWelcome);
        await SendMessage(ws, message);
        return ws;
    }

    internal static async Task SendMessage(WebSocket ws, string message)
        => await ws.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, CancellationToken.None);

    internal static async Task<WsMessage?> Receive(WebSocket ws)
    {
        var ct = new CancellationTokenSource(2500);

        using var stream = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, ct.Token);
            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        var json = Encoding.UTF8.GetString(stream.ToArray());
        var ret = JsonConvert.DeserializeObject<WsMessage>(json);
        if (ret is not null)
            ret.Raw = json;

        return ret;
    }

    internal static string CreateMessage(WsMessage.MessageType type, string? method, object? args = null)
    {
        var message = new WsMessage(type, method, args is null ? null : JToken.FromObject(args));
        return JsonConvert.SerializeObject(message, new JsonSerializerSettings() { ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() } });
    }


    internal static async Task<WebSocket> GetWebSocket(bool ignoreWelcome = true)
    {
        var ws = await _webSocketClient.ConnectAsync(new("ws://localhost:5111/ws"), CancellationToken.None);
        if (ignoreWelcome)
            await Receive(ws);
        return ws;
    }
}
