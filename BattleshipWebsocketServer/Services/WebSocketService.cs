using BattleshipWebsocketServer.Models;
using BattleshipWebsocketServer.Models.Public;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using System.Text;

namespace BattleshipWebsocketServer.Services;

public class WebSocketService
{
    private static string[] _events = new[] { "room.oncreate", "room.onkick", "room.ondestroy", "room.onjoin", "room.onstatechange", "room.onleave", "game.onshoot", "room.onmessage" };

    private Dictionary<string, Func<WebSocket, WsMessage, Task>> _requestMethods = new();
    private PlayersService _players;
    private RoomsService _rooms;

    public WebSocketService(PlayersService players, RoomsService rooms)
    {
        _players = players;
        _rooms = rooms;

        _rooms.Init(this);

        _requestMethods.Add("methods", Methods);
        _requestMethods.Add("ping", Ping);
        _requestMethods.Add("login", Login);
        _requestMethods.Add("room.list", RoomList);
        _requestMethods.Add("room.create", RoomCreate);
        _requestMethods.Add("room.join", RoomJoin);
        _requestMethods.Add("room.leave", RoomLeave);
        _requestMethods.Add("room.challenge", RoomChallenge);
        _requestMethods.Add("room.sendmessage", RoomSendMessage);
        _requestMethods.Add("game.placeships", GamePlaceShips);
        _requestMethods.Add("game.resetships", GameResetShips);
        _requestMethods.Add("game.shoot", GameShoot);
        _requestMethods.Add("game.surrender", GameSurrender);
        _requestMethods.Add("logout", Logout);
    }

    public async Task ProcessAsync(WebSocket ws)
    {
        await SendWelcome(ws);

        while (!ws.CloseStatus.HasValue && ws.State != WebSocketState.Aborted)
        {
            var message = await Read(ws);
            if (message is null)
                await Send(ws, new(WsMessage.MessageType.Error, null, JToken.FromObject("Failed to parse json")) { Comment = "Did you forget to JSON.stringify()?" });
            else if (message.Type == WsMessage.MessageType.Request)
            {
                var method = message.Method?.ToLower() ?? "";
                if (_requestMethods.ContainsKey(method))
                    await _requestMethods[method](ws, message);
                else
                    await Send(ws, new(WsMessage.MessageType.Error, method, JToken.FromObject($"Method \"{method}\" not found"))
                    {
                        Comment = $"Available methods: {string.Join(", ", _requestMethods.Keys)}"
                    });
            }
        }

        await _players.Logout(ws);
    }

    private async Task Methods(WebSocket ws, WsMessage message)
        => await Send(ws, message.Response(new { methods = _requestMethods.Keys, events = _events }));

    private async Task Ping(WebSocket ws, WsMessage message)
        => await Send(ws, message.Response("pong"));

    /// <summary>
    /// Login with a nickname, args: { "nickname":string }
    /// </summary>
    private async Task Login(WebSocket ws, WsMessage message)
    {
        if (message.Args is null || message.Args.Type != JTokenType.Object)
        {
            await Send(ws, message.Response(false, "Type of \"args\" must be object"));
            return;
        }
        if (!(message.Args?.Value<JObject>()?.ContainsKey("nickname") ?? false))
        {
            await Send(ws, message.Response(false, "\"args\" must contain \"nickname\" of type string"));
            return;
        }

        var res = _players.Register(ws, message.Args.Value<string>("nickname") ?? "");
        if (res.success && res.player is not null)
        {
            if (message.Args.Value<JObject>()?.ContainsKey("color") ?? false)
                res.player.Color = message.Args.Value<JToken>("color")?.ToString();

            await Send(ws, message.Response(res.success, res.message, new { player = new PlayerPublic(res.player) },
                "Nice, you loggined in, now you can execute one of the following methods:\n" +
                "room.list - will response to you with paginated rooms, use { page:integer } in order to get next pages, 0 is the first page\n" +
                "room.create - will create a new room and will put you in this room as owner, also will unsubscribe you from room update feed. args: none\n" +
                "room.join - you will join specified room. args: { roomId:integer }\n" +
                "logout - to logout"));
            Console.WriteLine("New player joined: " + message.Args.Value<string>("nickname"));
            if (res.player is not null)
                _rooms.SubscribeToUpdates(res.player);
        }
        else
            await Send(ws, message.Response(res.success, res.message));
    }

    /// <summary>
    /// Sends back list of available rooms, no args
    /// </summary>
    private async Task RoomList(WebSocket ws, WsMessage message)
        => await CheckLogin(ws, message, async player =>
        {
            int page = 0;
            if (message.Args is not null && message.Args.Type == JTokenType.Object)
            {
                var obj = message.Args.Value<JObject>();
                if (obj!.ContainsKey("page"))
                    int.TryParse(obj.Value<string>("page"), out page);
            }

            int skip = page * 20;
            var rooms = _rooms.Rooms.Skip(skip).Select(r => new RoomPublic(r, player));
            int totalPages = (int)Math.Ceiling(_rooms.Rooms.Count / 20d);

            await Send(ws, message.Response(true, null, new { rooms = rooms, page = page, totalPages = totalPages },
                "Use \"room.list\" with args { page:integer } to get rooms on specific page.\nUse \"room.join\" with { roomId:integer } args to join any room."));
        });

    /// <summary>
    /// Checks if player is logged and if so tries create room
    /// </summary>
    private async Task RoomCreate(WebSocket ws, WsMessage message)
        => await CheckLogin(ws, message, async (player) =>
        {
            var res = await _rooms.CreateRoom(player);
            if (res.success && res.room is not null)
                await Send(ws, message.Response(res.success, res.message, new { room = new RoomPublic(res.room, player) },
                    "If you want to leave room use \"room.leave\" without args, it will also destroy this room."));
            else await Send(ws, message.Response(res.success, res.message));
        });

    /// <summary>
    /// If player logged in joins to room specified in args: { roomId:number }
    /// </summary>
    private async Task RoomJoin(WebSocket ws, WsMessage message)
        => await CheckLogin(ws, message, async (player) =>
        {
            if (message.Args is null)
            {
                await Send(ws, message.Response(false, "\"args\" must not be null"));
                return;
            }
            if (message.Args.Type != JTokenType.Object)
            {
                await Send(ws, message.Response(false, "\"args\" must be an object"));
                return;
            }
            var args = message.Args.Value<JObject>();
            if (args is null || !args.ContainsKey("roomId") || !int.TryParse(args.Value<string>("roomId"), out int roomId))
            {
                await Send(ws, message.Response(false, "\"args\" must contain \"roomId\" of type integer"));
                return;
            }

            var res = await _rooms.JoinRoom(player, roomId);
            if (res.success && res.room is not null)
            {
                await Send(ws, message.Response(res.success, res.message, new { room = new RoomPublic(res.room, player) },
                    "Right now you`re a viewer, if you want challenge owner of this room execute \"room.challenge\" without args (if room`s state is idle) " +
                    "or \"room.leave\" without args to leave"));
                Console.WriteLine($"{player.Nickname} has joined room ID#{res.room.Id} owned by {res.room.Owner.Nickname}");
            }
            else await Send(ws, message.Response(res.success, res.message));
        });

    private async Task RoomLeave(WebSocket ws, WsMessage message)
        => await CheckLogin(ws, message, async player =>
        {
            var res = await _rooms.LeaveRoom(player);
            await Send(ws, message.Response(res.success, res.message, null,
                res.success ? "You now subscribed back to rooms updates" : null));
        });

    /// <summary>
    /// If player in a room and opponent of this room is null you can challenge owner of this room, args: none
    /// </summary>
    private async Task RoomChallenge(WebSocket ws, WsMessage message)
        => await CheckLogin(ws, message, async (player) =>
        {
            var res = await _rooms.Challenge(player);
            if (res.success && res.room is not null)
                await Send(ws, message.Response(res.success, res.message, new { room = new RoomPublic(res.room, player) },
                    $"Waiting from you to execute \"game.placeships\" method with {{ ships:Ship[] }} args where Ship is {{ x:integer, y:integer, size:integer, isVertical:boolean }}. " +
                    $"Amount of ships must be {Board.Sizes.Length} and their sizes are: {string.Join(", ", Board.Sizes)}"));
            else await Send(ws, message.Response(res.success, res.message));
        });

    /// <summary>
    /// Trying to place ships, args should contain ships key that array of type Ship. args: { ships:Ship[] }
    /// </summary>
    private async Task GamePlaceShips(WebSocket ws, WsMessage message)
        => await CheckLogin(ws, message, async player =>
        {
            if (message.Args is null)
            {
                await Send(ws, message.Response(false, "\"args\" must not be null"));
                return;
            }
            if (message.Args.Type != JTokenType.Object)
            {
                await Send(ws, message.Response(false, "\"args\" must be an object"));
                return;
            }
            var args = message.Args.Value<JObject>();
            if (!args?.ContainsKey("ships") ?? false)
            {
                await Send(ws, message.Response(false, "\"args\" must contain \"ships\" of type array of Ship"));
                return;
            }
            if (args?.Value<JToken>("ships")?.Type != JTokenType.Array)
            {
                await Send(ws, message.Response(false, $"\"ships\" must be an array, \"{args?.Value<JToken>("ships")?.Type.ToString()?.ToLower()}\" received"));
                return;
            }

            Ship[] ships;
            try
            {
                var jArr = message.Args?.Value<JArray>("ships") ?? new JArray();
                ships = JsonConvert.DeserializeObject<Ship[]>(jArr.ToString()) ?? Array.Empty<Ship>();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await Send(ws, message.Response(false, "Failed to parse ships. Ships must be an array of { x:integer, y:integer, size:integer, isVertical:boolean }"));
                return;
            }

            var res = await _rooms.PlaceShips(player, ships);
            if (res.success && res.room is not null)
                await Send(ws, message.Response(res.success, res.message, new { room = new RoomPublic(res.room, player) },
                    "Now wait for room to notify you that it in active state. If player want to move ships then execute \"game.resetships\" method, it will make him not ready to play. " +
                    "When both players is ready use \"game.shoot\" with { x:integer, y:integer } to shoot."));
            else await Send(ws, message.Response(res.success, res.message));
        });

    /// <summary>
    /// Resets board: clear cells and empty ships array. no args
    /// </summary>
    private async Task GameResetShips(WebSocket ws, WsMessage message)
        => await CheckLogin(ws, message, async player =>
        {
            var res = await _rooms.ResetShips(player);
            await Send(ws, message.Response(res.success, res.message, res.room is null ? null : new { room = new RoomPublic(res.room, player) }));
        });

    /// <summary>
    /// Trying to shoot ships. args: { x:number, y:number }
    /// </summary>
    /// <param name="ws"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    private async Task GameShoot(WebSocket ws, WsMessage message)
        => await CheckLogin(ws, message, async player =>
        {
            if (message.Args is null)
            {
                await Send(ws, message.Response(false, "\"args\" must not be null"));
                return;
            }
            if (message.Args.Type != JTokenType.Object)
            {
                await Send(ws, message.Response(false, "\"args\" must be an object with \"x\" and \"y\" with type of integers"));
                return;
            }
            var args = message.Args.Value<JObject>();
            if (args is null || !args.ContainsKey("x") || !args.ContainsKey("y") || !int.TryParse(args.Value<string>("x"), out int x) || !int.TryParse(args.Value<string>("y"), out int y))
            {
                await Send(ws, message.Response(false, "\"args\" must contain \"x\" and \"y\" of type integer"));
                return;
            }

            var res = await _rooms.Shoot(player, x, y);
            if (res.success && res.room is not null)
            {
                await Send(ws, message.Response(res.success, res.message, new
                {
                    x = x,
                    y = y,
                    isHit = res.isHit,
                    sunkenShip = res.sunkenShip is null ? null : new ShipPublic(res.sunkenShip),
                    room = new RoomPublic(res.room, player)
                }));

                if (res.isGameOver)
                {
                    var data = new
                    {
                        winner = res.isOwnerWon ? res.room.Owner : res.room.Opponent,
                        isOwnerWon = res.isOwnerWon,
                        owner = new BoardPublic(res.room.OwnerBoard, false),
                        opponent = res.room.OpponentBoard is null ? null : new BoardPublic(res.room.OpponentBoard, false)
                    };

                    await Send(res.room.Owner.Ws, new(WsMessage.MessageType.Event, "game.ongameover", JToken.FromObject(data)));
                    if (res.room.Opponent is not null)
                        await Send(res.room.Opponent.Ws, new(WsMessage.MessageType.Event, "game.ongameover", JToken.FromObject(data)));
                    foreach (var viewer in res.room.Viewers.ToArray())
                        await Send(viewer.Ws, new(WsMessage.MessageType.Event, "game.ongameover", JToken.FromObject(data)));

                    res.room.End();

                    await Send(res.room.Owner.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(res.room, res.room.Owner) })));
                    if (res.room.Opponent is not null)
                        await Send(res.room.Opponent.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(res.room, res.room.Opponent) })));
                    foreach (var viewer in res.room.Viewers.ToArray())
                        await Send(viewer.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(res.room, viewer) })));
                }
            }
            else await Send(ws, message.Response(res.success, res.message));
        });

    /// <summary>
    /// If player is owner or opponent and game in active state, player can surrender, game will change it state to end and game.ongameover event will send
    /// </summary>
    private async Task GameSurrender(WebSocket ws, WsMessage message)
        => await CheckLogin(ws, message, async player =>
        {
            var res = await _rooms.Surrender(player);
            await Send(ws, message.Response(res.success, res.message));
        });

    /// <summary>
    /// If player is in the room, send chat message. args: { message:string }
    /// </summary>
    private async Task RoomSendMessage(WebSocket ws, WsMessage wsMessage)
        => await CheckLogin(ws, wsMessage, async player =>
        {
            if (wsMessage.Args is null)
            {
                await Send(ws, wsMessage.Response(false, "\"args\" must not be null"));
                return;
            }
            if (wsMessage.Args.Type != JTokenType.Object)
            {
                await Send(ws, wsMessage.Response(false, "\"args\" must be an object with \"message\" of type string"));
                return;
            }
            var args = wsMessage.Args.Value<JObject>();
            if (args is null || !args.ContainsKey("message"))
            {
                await Send(ws, wsMessage.Response(false, "\"args\" must contain \"message\" of type string"));
                return;
            }
            var message = args?.Value<string>("message");
            if (message is null)
            {
                await Send(ws, wsMessage.Response(false, "\"message\" was null"));
                return;
            }
            var res = await _rooms.SendMessage(player, message);
            await Send(ws, wsMessage.Response(res.success, res.message, new { chatMessage = res.chatMessage is null ? null : new ChatMessagePublic(res.chatMessage) }));
        });

    /// <summary>
    /// Logout, no args
    /// </summary>
    private async Task Logout(WebSocket ws, WsMessage message)
    {
        var res = await _players.Logout(ws);
        await Send(ws, message.Response(res.success, res.message));
    }

    private async Task CheckLogin(WebSocket ws, WsMessage message, Func<Player, Task> callback)
    {
        if (!_players.Players.ContainsKey(ws))
        {
            await Send(ws, new(WsMessage.MessageType.NotAuthorised, message.Method));
            return;
        }
        await callback(_players.Players[ws]);
    }

    private async Task<WsMessage?> Read(WebSocket ws)
    {
        try
        {
            var buffer = new byte[1024 * 4];
            await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var json = Encoding.UTF8.GetString(buffer);
            var ret = JsonConvert.DeserializeObject<WsMessage>(json);
            if (ret is not null) ret.Raw = json;
            return ret;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("at WebSocketService.Read(WebSocket): " + ex.Message);
            return null;
        }
    }

    private async Task SendWelcome(WebSocket ws)
    {
        string welcome = "Hello, this is the websocket endpoint for the Battleship game.\n" +
                         "Communication occurs through the object { \"type\":string, \"method\":string|null, \"args\":object|string, \"comment\":string|undefined }.\n" +
                         "Type can be \"welcome\" (only first message from server when you just connect), \"request\", \"event\", \"response\", \"notauthorised\" or \"error\".\n" +
                         "Method is a name of the method you want to execute, it might be null if error thrown.\n" +
                         "Args is where data will be, it can be object or string\n" +
                         "Comment may contain data of what to do next.\n" +
                         "You can execute \"methods\" method to get all server methods and event (`ws.send(JSON.stringify({ \"type\":\"request\", \"method\":\"methods\" }));`)\n" +
                         "Right now we are waiting for you to execute login method, example: { \"type\":\"request\", \"method\":\"login\", \"args\": { \"nickname\":\"Player\" } }\n";
        await Send(ws, new(WsMessage.MessageType.Welcome, null, JToken.FromObject(welcome)));
    }

    public async Task Send(WebSocket ws, WsMessage message)
    {
        try
        {
            object data;

            if (message.Comment is null) data = new { type = message.Type.ToString().ToLower(), method = message.Method, args = message.Args };
            else data = new { type = message.Type.ToString().ToLower(), method = message.Method, args = message.Args, comment = message.Comment };
            string json = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch { }
    }
}
