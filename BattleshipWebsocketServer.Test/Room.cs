namespace BattleshipWebsocketServer.Tests;

using BattleshipWebsocketServer.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

public class Room
{
    private readonly ITestOutputHelper _output;

    public Room(ITestOutputHelper output)
    {
        _output = output;
        Util.ResetApp();
    }

    [Fact]
    public async Task EmptyList()
    {
        // Get list being unauthorized
        var message = Util.CreateMessage(WsMessage.MessageType.Request, "room.list");
        var ws = await Util.SendMessage(message);
        var res = await Util.Receive(ws);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.NotAuthorised, res?.Type);
        Assert.Equal("room.list", res?.Method);

        // Logging in
        await Util.LogIn(ws, "Test3");

        // Get empry list
        message = Util.CreateMessage(WsMessage.MessageType.Request, "room.list");
        await Util.SendMessage(ws, message);
        res = await Util.Receive(ws);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("room.list", res?.Method);

        var args = res?.Args?.Value<JObject>();
        Assert.True(args?.ContainsKey("rooms"));
        Assert.Equal(JTokenType.Array, args?.Value<JToken>("rooms")?.Type);
        Assert.Equal(0, args?.Value<JArray>("rooms")?.Count);
    }

    [Fact]
    public async Task Create()
    {
        // Create 2 players, one will create room, second will listen for event
        string player1 = "Test4";
        string player2 = "Test5";

        var ws1 = await Util.LogIn(player1);
        var ws2 = await Util.LogIn(player2);

        // Check room creation
        var message = Util.CreateMessage(WsMessage.MessageType.Request, "room.create");
        await Util.SendMessage(ws1, message);
        var res = await Util.Receive(ws1);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("room.create", res?.Method);
        dynamic? args = res?.Args;
        Assert.NotNull(args);
        Assert.NotNull(args?.room);
        Assert.NotNull(args?.room?.id);
        Assert.NotNull((int?)args?.room?.id);
        Assert.NotNull(args?.room?.owner);
        Assert.NotNull(args?.room?.owner?.board);
        Assert.NotNull(args?.room?.owner?.ships);
        Assert.NotNull(args?.room?.owner?.player);
        Assert.NotNull(args?.room?.owner?.player?.id);
        Assert.NotNull(args?.room?.owner?.player?.nickname);
        Assert.Equal(player1, (string?)args?.room?.owner?.player?.nickname);
        Assert.NotNull(args?.room?.owner?.isReady);
        Assert.Equal(false, (bool?)args?.room?.owner?.isReady);
        Assert.NotNull(args?.room?.opponent);
        Assert.Equal(JTokenType.Null, args?.room?.opponent?.Type);
        Assert.NotNull(args?.room?.state);
        Assert.Equal("idle", (string?)args?.room?.state);
        Assert.NotNull(args?.room?.isOwnerTurn);
        Assert.NotNull(args?.room?.viewers);

        // Check event
        res = await Util.Receive(ws2);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.Event, res?.Type);
        Assert.Equal("room.oncreate", res?.Method);
        dynamic? eventArgs = res?.Args;
        Assert.NotNull(eventArgs);
        Assert.NotNull(eventArgs?.room);
        Assert.NotNull(eventArgs?.room?.id);
        Assert.NotNull((int?)eventArgs?.room?.id);
        Assert.NotNull(eventArgs?.room?.owner);
        Assert.NotNull(eventArgs?.room?.owner?.board);
        Assert.NotNull(eventArgs?.room?.owner?.ships);
        Assert.NotNull(eventArgs?.room?.owner?.player);
        Assert.NotNull(eventArgs?.room?.owner?.player?.id);
        Assert.NotNull(eventArgs?.room?.owner?.player?.nickname);
        Assert.Equal(player1, (string?)eventArgs?.room?.owner?.player?.nickname);
        Assert.NotNull(eventArgs?.room?.owner?.isReady);
        Assert.Equal(false, (bool?)eventArgs?.room?.owner?.isReady);
        Assert.NotNull(eventArgs?.room?.opponent);
        Assert.Equal(JTokenType.Null, eventArgs?.room?.opponent?.Type);
        Assert.NotNull(eventArgs?.room?.state);
        Assert.Equal("idle", (string?)eventArgs?.room?.state);
        Assert.NotNull(eventArgs?.room?.isOwnerTurn);
        Assert.NotNull(eventArgs?.room?.viewers);

        // Compare objects
        Assert.Equal(args?.room, eventArgs?.room);

        // Check list

    }
}
