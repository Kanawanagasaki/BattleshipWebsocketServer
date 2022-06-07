namespace BattleshipWebsocketServer.Tests;

using BattleshipWebsocketServer.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class Auth : Util
{
    public Auth(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task Connect()
    {
        var ws = await GetWebSocket(false);
        Assert.Equal(WebSocketState.Open, ws.State);

        var res = await Receive(ws);
        Assert.Equal(WsMessage.MessageType.Welcome, res?.Type);
    }

    [Fact]
    public async Task Login()
    {
        // Testing invalid messages
        var invalidMessages = new[]
        {
            CreateMessage(WsMessage.MessageType.Request, "login"),
            CreateMessage(WsMessage.MessageType.Request, "login", "test"),
            CreateMessage(WsMessage.MessageType.Request, "login", new { }),
            CreateMessage(WsMessage.MessageType.Request, "login", new { nickname = "Invalid nickname" }),
            CreateMessage(WsMessage.MessageType.Request, "login", new { nickname = "Привет!!" }),
            CreateMessage(WsMessage.MessageType.Request, "login", new { nickname = "1" }),
            CreateMessage(WsMessage.MessageType.Request, "login", new { nickname = "01234567890123456789012345678901234567890123456789" })
        };

        var ws = await GetWebSocket();

        foreach (var invalidMessage in invalidMessages)
        {
            await SendMessage(ws, invalidMessage);
            var data = await Receive(ws);

            Assert.Equal(WsMessage.MessageType.Response, data?.Type);
            Assert.Equal("login", data?.Method);

            var args = data?.Args?.Value<JObject>();
            Assert.True(args?.ContainsKey("success"), invalidMessage);
            Assert.False(args?.Value<bool>("success"), invalidMessage);
        }

        // Testing successfull login
        var nickname = "Test1";

        var message = CreateMessage(WsMessage.MessageType.Request, "login", new { nickname = nickname });
        await SendMessage(ws, message);
        var res = await Receive(ws);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("login", res?.Method);

        var obj = res?.Args?.Value<JObject>();
        Assert.True(obj?.ContainsKey("success"));
        Assert.True(obj?.Value<bool>("success"));
        Assert.True(obj?.ContainsKey("player"));

        var player = obj?.Value<JObject>("player");
        Assert.True(player?.ContainsKey("id"));
        Assert.True(int.TryParse(player?.Value<string>("id"), out _));
        Assert.True(player?.ContainsKey("nickname"));
        Assert.Equal(nickname, player?.Value<string>("nickname"));

        // Testing taken nickname
        await SendMessage(ws, message);
        res = await Receive(ws);

        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("login", res?.Method);

        obj = res?.Args?.Value<JObject>();
        Assert.True(obj?.ContainsKey("success"));
        Assert.False(obj?.Value<bool>("success"));
    }

    [Fact]
    public async Task MultipleLogins()
    {
        var nickname = "Test6";

        var message = CreateMessage(WsMessage.MessageType.Request, "login", new { nickname = nickname });
        var ws1 = await SendMessage(message);
        var res = await Receive(ws1);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("login", res?.Method);

        var obj = res?.Args?.Value<JObject>();
        Assert.True(obj?.ContainsKey("success"));
        Assert.True(obj?.Value<bool>("success"));
        Assert.True(obj?.ContainsKey("player"));

        message = CreateMessage(WsMessage.MessageType.Request, "login", new { nickname = nickname });
        var ws2 = await SendMessage(message);
        res = await Receive(ws2);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("login", res?.Method);

        obj = res?.Args?.Value<JObject>();
        Assert.True(obj?.ContainsKey("success"));
        Assert.False(obj?.Value<bool>("success"));
        Assert.False(obj?.ContainsKey("player"));
    }

    [Fact]
    public async Task Logout()
    {
        // Testing not loggined
        var message = CreateMessage(WsMessage.MessageType.Request, "logout");
        var ws = await SendMessage(message);
        var res = await Receive(ws);

        Assert.NotNull(res);

        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("logout", res?.Method);

        var obj = res?.Args?.Value<JObject>();
        Assert.True(obj?.ContainsKey("success"));
        Assert.False(obj?.Value<bool>("success"));

        // Login to test
        var nickname = "Test2";

        message = CreateMessage(WsMessage.MessageType.Request, "login", new { nickname = nickname });
        await SendMessage(ws, message);
        res = await Receive(ws);

        Assert.NotNull(res);

        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("login", res?.Method);

        obj = res?.Args?.Value<JObject>();
        Assert.True(obj?.ContainsKey("success"));
        Assert.True(obj?.Value<bool>("success"));

        // Logouting
        message = CreateMessage(WsMessage.MessageType.Request, "logout");
        await SendMessage(ws, message);
        res = await Receive(ws);

        Assert.NotNull(res);

        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("logout", res?.Method);

        obj = res?.Args?.Value<JObject>();
        Assert.True(obj?.ContainsKey("success"));
        Assert.True(obj?.Value<bool>("success"));

        // Trying to logout again
        message = CreateMessage(WsMessage.MessageType.Request, "logout");
        await SendMessage(ws, message);
        res = await Receive(ws);

        Assert.NotNull(res);

        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("logout", res?.Method);

        obj = res?.Args?.Value<JObject>();
        Assert.True(obj?.ContainsKey("success"));
        Assert.False(obj?.Value<bool>("success"));
    }
}
