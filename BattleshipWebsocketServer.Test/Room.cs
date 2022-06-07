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

public class Room : Util
{
    public Room(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task EmptyList()
    {
        // Get list being unauthorized
        var message = CreateMessage(WsMessage.MessageType.Request, "room.list");
        var ws = await SendMessage(message);
        var res = await Receive(ws);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.NotAuthorised, res?.Type);
        Assert.Equal("room.list", res?.Method);

        // Logging in
        await LogIn(ws, "Test3");

        // Get empry list
        message = CreateMessage(WsMessage.MessageType.Request, "room.list");
        await SendMessage(ws, message);
        res = await Receive(ws);
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

        var ws1 = await LogIn(player1);
        var ws2 = await LogIn(player2);

        // Check room creation
        var message = CreateMessage(WsMessage.MessageType.Request, "room.create");
        await SendMessage(ws1, message);
        var res = await Receive(ws1);
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
        Assert.Empty(args?.room?.viewers);
        Assert.NotNull(args?.room?.messages);
        Assert.Empty(args?.room?.messages);

        // Check event
        res = await Receive(ws2);
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
        Assert.Empty(eventArgs?.room?.viewers);
        Assert.NotNull(eventArgs?.room?.messages);
        Assert.Empty(eventArgs?.room?.messages);

        // Compare objects
        Assert.Equal(args?.room, eventArgs?.room);

        // Check list
        message = CreateMessage(WsMessage.MessageType.Request, "room.list");
        await SendMessage(ws1, message);
        res = await Receive(ws1);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("room.list", res?.Method);
        dynamic? listArgs = res?.Args;
        Assert.NotNull(listArgs);
        Assert.NotNull(listArgs?.success);
        Assert.True((bool)listArgs?.success);
        Assert.NotNull(listArgs?.rooms);
        Assert.Equal(JTokenType.Array, listArgs?.rooms?.Type);
        Assert.Equal(1, listArgs?.rooms?.Count);
        Assert.Equal(args?.room, listArgs?.rooms[0]);

        await SendMessage(ws2, message);
        res = await Receive(ws2);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("room.list", res?.Method);
        listArgs = res?.Args;
        Assert.NotNull(listArgs);
        Assert.NotNull(listArgs?.success);
        Assert.True((bool)listArgs?.success);
        Assert.NotNull(listArgs?.rooms);
        Assert.Equal(JTokenType.Array, listArgs?.rooms?.Type);
        Assert.Equal(1, listArgs?.rooms?.Count);
        Assert.Equal(args?.room, listArgs?.rooms[0]);
    }

    [Fact]
    public async Task Join()
    {
        // Create 2 players, one will create room, second will join this room
        string player1 = "Test6";
        string player2 = "Test7";

        var ws1 = await LogIn(player1);
        var ws2 = await LogIn(player2);

        // Create room 1
        var message = CreateMessage(WsMessage.MessageType.Request, "room.create");
        await SendMessage(ws1, message);
        var res = await Receive(ws1);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("room.create", res?.Method);
        Assert.NotNull(res?.Args);
        Assert.True(res?.Args?.Value<bool>("success"));

        // Second player got event of room creation
        var res2 = await Receive(ws2);
        Assert.NotNull(res2);
        Assert.Equal(WsMessage.MessageType.Event, res2?.Type);
        Assert.Equal("room.oncreate", res2?.Method);
        Assert.NotNull(res2?.Args);
        Assert.Equal(res?.Args?.Value<JObject>("room"), res2?.Args?.Value<JObject>("room"));

        // Join room with second player
        message = CreateMessage(WsMessage.MessageType.Request, "room.join", new { roomId = res?.Args?.Value<JObject>("room")?.Value<int>("id") });
        await SendMessage(ws2, message);
        var res3 = await Receive(ws2);
        Assert.NotNull(res3);
        Assert.Equal(WsMessage.MessageType.Response, res3?.Type);
        Assert.Equal("room.join", res3?.Method);
        Assert.NotNull(res3?.Args);
        Assert.True(res3?.Args?.Value<bool>("success"));
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<int>("id"), res3?.Args?.Value<JObject>("room")?.Value<int>("id"));
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<JObject>("owner"), res3?.Args?.Value<JObject>("room")?.Value<JObject>("owner"));
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<JObject>("opponent"), res3?.Args?.Value<JObject>("room")?.Value<JObject>("opponent"));
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<string>("state"), res3?.Args?.Value<JObject>("room")?.Value<string>("state"));
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<bool>("isOwnerTurn"), res3?.Args?.Value<JObject>("room")?.Value<bool>("isOwnerTurn"));
        Assert.NotEqual(res?.Args?.Value<JObject>("room")?.Value<JArray>("viewers"), res3?.Args?.Value<JObject>("room")?.Value<JArray>("viewers"));
        Assert.Empty(res3?.Args?.Value<JObject>("room")?.Value<JArray>("messages"));

        // First player got event of second player joining the room
        var res4 = await Receive(ws1);
        Assert.NotNull(res4);
        Assert.Equal(WsMessage.MessageType.Event, res4?.Type);
        Assert.Equal("room.onjoin", res4?.Method);
        Assert.NotNull(res4?.Args);
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<int>("id"), res4?.Args?.Value<JObject>("room")?.Value<int>("id"));
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<JObject>("owner"), res4?.Args?.Value<JObject>("room")?.Value<JObject>("owner"));
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<JObject>("opponent"), res4?.Args?.Value<JObject>("room")?.Value<JObject>("opponent"));
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<string>("state"), res4?.Args?.Value<JObject>("room")?.Value<string>("state"));
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<bool>("isOwnerTurn"), res4?.Args?.Value<JObject>("room")?.Value<bool>("isOwnerTurn"));
        Assert.NotEqual(res?.Args?.Value<JObject>("room")?.Value<JArray>("viewers"), res4?.Args?.Value<JObject>("room")?.Value<JArray>("viewers"));
        Assert.Empty(res4?.Args?.Value<JObject>("room")?.Value<JArray>("messages"));
        Assert.Equal(player2, res4?.Args?.Value<JObject>("player")?.Value<string>("nickname"));
    }

    [Fact]
    public async Task Destroy()
    {
        // Create 3 players, one will create room, second will listen for event, third will listen for room.oncreate and room.ondestroy event
        string player1 = "Test8";
        string player2 = "Test9";
        string player3 = "Test10";

        var ws1 = await LogIn(player1);
        var ws2 = await LogIn(player2);
        var ws3 = await LogIn(player3);

        // Create room 1
        var message = CreateMessage(WsMessage.MessageType.Request, "room.create");
        await SendMessage(ws1, message);
        var res = await Receive(ws1);
        Assert.NotNull(res);
        Assert.Equal(WsMessage.MessageType.Response, res?.Type);
        Assert.Equal("room.create", res?.Method);
        Assert.NotNull(res?.Args);
        Assert.True(res?.Args?.Value<bool>("success"));

        // Skpi room.oncreate event
        await Receive(ws2);
        await Receive(ws3);

        // Join room with second player
        message = CreateMessage(WsMessage.MessageType.Request, "room.join", new { roomId = res?.Args?.Value<JObject>("room")?.Value<int>("id") });
        await SendMessage(ws2, message);
        var res3 = await Receive(ws2);
        Assert.NotNull(res3);
        Assert.Equal(WsMessage.MessageType.Response, res3?.Type);
        Assert.Equal("room.join", res3?.Method);
        Assert.NotNull(res3?.Args);
        Assert.True(res3?.Args?.Value<bool>("success"));

        // Skip room.onjoin event
        await Receive(ws1);

        // Destroying the room
        message = CreateMessage(WsMessage.MessageType.Request, "room.leave");
        await SendMessage(ws1, message);

        // Second player got room.onkick event
        var res4 = await Receive(ws2);
        Assert.NotNull(res4);
        Assert.Equal(WsMessage.MessageType.Event, res4?.Type);
        Assert.Equal("room.onkick", res4?.Method);
        Assert.NotNull(res4?.Args);
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<int>("id"), res4?.Args?.Value<int>("roomId"));

        // First player got room.onleave event from Second player
        var res5 = await Receive(ws1);
        Assert.NotNull(res5);
        Assert.Equal(WsMessage.MessageType.Event, res5?.Type);
        Assert.Equal("room.onleave", res5?.Method);
        Assert.NotNull(res5?.Args);
        Assert.Equal(player2, res5?.Args?.Value<JObject>("player")?.Value<string>("nickname"));

        // Response that first player left the room
        var res6 = await Receive(ws1);
        Assert.NotNull(res6);
        Assert.Equal(WsMessage.MessageType.Response, res6?.Type);
        Assert.Equal("room.leave", res6?.Method);
        Assert.NotNull(res6?.Args);
        Assert.True(res6?.Args?.Value<bool>("success"));

        // Third player received room.ondestroy event
        var res7 = await Receive(ws3);
        Assert.NotNull(res7);
        Assert.Equal(WsMessage.MessageType.Event, res7?.Type);
        Assert.Equal("room.ondestroy", res7?.Method);
        Assert.NotNull(res7?.Args);
        Assert.Equal(res?.Args?.Value<JObject>("room")?.Value<int>("id"), res7?.Args?.Value<int>("roomId"));
    }
}
