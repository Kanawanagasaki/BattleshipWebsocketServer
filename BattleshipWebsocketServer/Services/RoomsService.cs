using BattleshipWebsocketServer.Models;
using BattleshipWebsocketServer.Models.Public;
using Newtonsoft.Json.Linq;

namespace BattleshipWebsocketServer.Services;

public class RoomsService
{
    private List<Room> _rooms = new();
    public IReadOnlyList<Room> Rooms => _rooms;

    private List<Player> _updateSubscribers = new();

    private WebSocketService? _webSocket;

    public void Init(WebSocketService webSocket) => _webSocket = webSocket;

    public async Task<(bool success, string message, Room? room)> CreateRoom(Player player)
    {
        if (_rooms.Any(r => r.Owner == player || r.Opponent == player || r.Viewers.Contains(player)))
            return (false, "You cannot create a new room while in another room", null);

        var room = new Room(player);
        _rooms.Add(room);
        Console.WriteLine(player.Nickname + " created room ID#" + room.Id);

        List<Task> tasks = new();

        lock (_updateSubscribers)
        {
            if (_updateSubscribers.Contains(player))
                _updateSubscribers.Remove(player);

            foreach (var subscriber in _updateSubscribers)
                if (subscriber != player && _webSocket is not null && subscriber is not null)
                    tasks.Add(_webSocket.Send(subscriber.Ws, new(WsMessage.MessageType.Event, "room.oncreate", JToken.FromObject(new { room = new RoomPublic(room, subscriber) }))));
        }

        await Task.WhenAll(tasks);

        return (true, "", room);
    }

    public async Task<(bool success, string message)> DestroyRoom(Room room)
    {
        var tasks = new List<Task>();

        lock (_rooms)
        {
            if (!_rooms.Contains(room))
                return (false, "Room not found");

            if (room.Opponent is not null)
            {
                var opponent = room.Opponent;
                tasks.Add(LeaveRoom(room.Opponent));
                tasks.Add(_webSocket!.Send(opponent.Ws, new(WsMessage.MessageType.Event, "room.onkick", JToken.FromObject(new { roomId = room.Id }))));
            }
            foreach (var viewer in room.Viewers.ToArray())
            {
                tasks.Add(LeaveRoom(viewer));
                tasks.Add(_webSocket!.Send(viewer.Ws, new(WsMessage.MessageType.Event, "room.onkick", JToken.FromObject(new { roomId = room.Id }))));
            }

            _rooms.Remove(room);
            Console.WriteLine(room.Owner.Nickname + "`s room with ID#" + room.Id + " destroyed");

            lock (_updateSubscribers)
            {
                foreach (var subscriber in _updateSubscribers)
                    if (_webSocket is not null && subscriber.Ws is not null)
                        tasks.Add(_webSocket.Send(subscriber.Ws, new(WsMessage.MessageType.Event, "room.ondestroy", JToken.FromObject(new { roomId = room.Id }))));

                if (!_updateSubscribers.Contains(room.Owner))
                    _updateSubscribers.Add(room.Owner);
            }
        }

        await Task.WhenAll(tasks);

        return (true, "");
    }

    public async Task<(bool success, string message, Room? room)> JoinRoom(Player player, int id)
    {
        if (_rooms.Any(r => r.Owner == player || r.Opponent == player || r.Viewers.Contains(player)))
            return (false, "You already in another room", null);

        var room = _rooms.FirstOrDefault(r => r.Id == id);
        if (room is null)
            return (false, "Room not found", null);

        if (room.Viewers.Contains(player))
            return (false, "You already in this room", null);

        lock (_updateSubscribers)
            if (_updateSubscribers.Contains(player))
                _updateSubscribers.Remove(player);

        room.Join(player);

        await _webSocket!.Send(room.Owner.Ws, new WsMessage(WsMessage.MessageType.Event, "room.onjoin", JToken.FromObject(new
        {
            room = new RoomPublic(room, room.Owner),
            player = new PlayerPublic(player)
        })));
        if (room.Opponent is not null && room.Opponent != player)
            await _webSocket!.Send(room.Opponent.Ws, new WsMessage(WsMessage.MessageType.Event, "room.onjoin", JToken.FromObject(new
            {
                room = new RoomPublic(room, room.Opponent),
                player = new PlayerPublic(player)
            })));
        foreach (var viewer in room.Viewers.ToArray())
            if (viewer != player)
                await _webSocket!.Send(viewer.Ws, new WsMessage(WsMessage.MessageType.Event, "room.onjoin", JToken.FromObject(new
                {
                    room = new RoomPublic(room, viewer),
                    player = new PlayerPublic(player)
                })));

        return (true, "", room);
    }

    public async Task<(bool success, string message)> LeaveRoom(Player player)
    {
        var room = GetJoinedRoom(player);
        if (room is null) return (false, "You didn't join any room");
        if (room.Owner == player)
            await DestroyRoom(room);
        else
        {
            bool flag = room.Opponent == player;

            room.Leave(player);

            if (flag)
            {
                await _webSocket!.Send(room.Owner.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, room.Owner) })));
                foreach (var viewer in room.Viewers.ToArray())
                    await _webSocket!.Send(viewer.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, viewer) })));
            }

            await _webSocket!.Send(room.Owner.Ws, new(WsMessage.MessageType.Event, "room.onleave", JToken.FromObject(new { player = new PlayerPublic(player) })));
            if (room.Opponent is not null && room.Opponent != player)
                await _webSocket!.Send(room.Opponent.Ws, new(WsMessage.MessageType.Event, "room.onleave", JToken.FromObject(new { player = new PlayerPublic(player) })));
            foreach (var viewer in room.Viewers.ToArray())
                if (viewer != player)
                    await _webSocket!.Send(viewer.Ws, new(WsMessage.MessageType.Event, "room.onleave", JToken.FromObject(new { player = new PlayerPublic(player) })));

            lock (_updateSubscribers)
                if (!_updateSubscribers.Contains(player))
                    _updateSubscribers.Add(player);
        }
        return (true, "");
    }

    public async Task<(bool success, string message, Room? room)> Challenge(Player player)
    {
        var room = GetJoinedRoom(player);
        if (room is null)
            return (false, "You didn't join any room", null);
        if (room.State != Room.States.Idle && room.State != Room.States.End)
            return (false, "You cannot challenge owner of this room because he already playing", null);
        if (room.Owner == player)
            return (false, "You cannot challenge yourself", null);

        room.Challenge(player);

        await _webSocket!.Send(room.Owner.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, room.Owner) })));
        foreach (var viewer in room.Viewers.ToArray())
            await _webSocket!.Send(viewer.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, viewer) })));

        return (true, "", room);
    }

    public async Task<(bool success, string message, Room? room)> PlaceShips(Player player, Ship[] ships)
    {
        var room = GetJoinedRoom(player);
        if (room is null)
            return (false, "You should be in room in order to place ships", null);
        if (room.Owner != player && room.Opponent != player)
            return (false, "You cannot place ships in room where you not playing", null);
        if (room.State != Room.States.Preparation)
            return (false, "You can place ships only when room in preparation state", null);

        var board = room.Owner == player ? room.OwnerBoard : room.Opponent == player ? room.OpponentBoard : null;
        if (board is null)
            return (false, "Internal error", null);

        if (!board.CheckShipSizes(ships))
            return (false, $"The size of the ships does not match the specified ({string.Join(", ", Board.Sizes)})", null);

        if (!board.TryPlaceShips(ships))
            return (false, "Failed to place ships", null);

        if (room.OwnerBoard.IsReady && (room.OpponentBoard?.IsReady ?? false))
            room.Activate();

        if (room.Owner != player)
            await _webSocket!.Send(room.Owner.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, room.Owner) })));
        if (room.Opponent is not null && room.Opponent != player)
            await _webSocket!.Send(room.Opponent.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, room.Opponent) })));
        foreach (var viewer in room.Viewers.ToArray())
            if (viewer != player)
                await _webSocket!.Send(viewer.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, viewer) })));

        return (true, "", room);
    }

    public async Task<(bool success, string message, Room? room)> ResetShips(Player player)
    {
        var room = GetJoinedRoom(player);
        if (room is null)
            return (false, "You should be in room in order to reset ships", null);
        if (room.Owner != player && room.Opponent != player)
            return (false, "You cannot reset ships in room where you not playing", null);
        if (room.State != Room.States.Preparation)
            return (false, "You can reset ships only when room in preparation state", null);

        var board = room.Owner == player ? room.OwnerBoard : room.Opponent == player ? room.OpponentBoard : null;
        if (board is null)
            return (false, "Internal error", null);

        board.Reset();

        if (room.Owner != player)
            await _webSocket!.Send(room.Owner.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, room.Owner) })));
        if (room.Opponent is not null && room.Opponent != player)
            await _webSocket!.Send(room.Opponent.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, room.Opponent) })));
        foreach (var viewer in room.Viewers.ToArray())
            if (viewer != player)
                await _webSocket!.Send(viewer.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, viewer) })));

        return (true, "", room);
    }

    public record SalvoResult(bool success = false, bool isHit = false, Ship? sunkenShip = null, Room? room = null, bool isGameOver = false, bool isOwnerWon = false, string message = "");
    public async Task<SalvoResult> Salvo(Player player, int x, int y)
    {
        var room = GetJoinedRoom(player);
        if (room is null)
            return new(message: "You should be in room in order make moves");
        if (room.Owner != player && room.Opponent != player)
            return new(message: "You cannot do moves in room where you not playing");
        if (room.State != Room.States.Active)
            return new(message: "You can make moves only when room in active state");
        if (room.IsOwnerTurn && room.Owner != player)
            return new(message: "It is your opponent`s turn");
        if (!room.IsOwnerTurn && room.Opponent != player)
            return new(message: "It is your opponent`s turn");

        var board = room.Owner == player ? room.OpponentBoard : room.Opponent == player ? room.OwnerBoard : null;
        if (board is null)
            return new(message: "Internal error");

        var res = board.Salvo(x, y);
        if (res.success)
        {
            if (!res.isHit)
                room.ToggleTurn();

            if (room.Owner != player)
                await _webSocket!.Send(room.Owner.Ws, new(WsMessage.MessageType.Event, "game.onsalvo", JToken.FromObject(new
                {
                    x = x,
                    y = y,
                    isHit = res.isHit,
                    sunkenShip = res.sunkenShip,
                    room = new RoomPublic(room, room.Owner)
                })));

            if (room.Opponent is not null && room.Opponent != player)
                await _webSocket!.Send(room.Opponent.Ws, new(WsMessage.MessageType.Event, "game.onsalvo", JToken.FromObject(new
                {
                    x = x,
                    y = y,
                    isHit = res.isHit,
                    sunkenShip = res.sunkenShip,
                    room = new RoomPublic(room, room.Opponent)
                })));
            foreach (var viewer in room.Viewers.ToArray())
                if (viewer != player)
                    await _webSocket!.Send(viewer.Ws, new(WsMessage.MessageType.Event, "game.onsalvo", JToken.FromObject(new
                    {
                        x = x,
                        y = y,
                        isHit = res.isHit,
                        sunkenShip = res.sunkenShip,
                        room = new RoomPublic(room, viewer)
                    })));


            if (board.Ships.All(s => s.IsDead))
                return new(res.success, res.isHit, res.sunkenShip, room, true, board.Player.Id != room.Owner.Id, res.message);
        }

        return new(res.success, res.isHit, res.sunkenShip, room, false, false, res.message);
    }

    public async Task<(bool success, string message)> Surrender(Player player)
    {
        var room = GetJoinedRoom(player);
        if (room is null)
            return (false, "You should be in room in order to surrender");
        if (room.Owner != player && room.Opponent != player)
            return (false, "You cannot surrender in room where you not playing");
        if (room.State != Room.States.Active)
            return (false, "You can surrender only when room in active state");

        var data = new
        {
            winner = room.Owner != player ? room.Owner : room.Opponent,
            isOwnerWon = room.Owner != player,
            owner = new BoardPublic(room.OwnerBoard, false),
            opponent = room.OpponentBoard is null ? null : new BoardPublic(room.OpponentBoard, false)
        };

        await _webSocket!.Send(room.Owner.Ws, new(WsMessage.MessageType.Event, "game.ongameover", JToken.FromObject(data)));
        if (room.Opponent is not null)
            await _webSocket!.Send(room.Opponent.Ws, new(WsMessage.MessageType.Event, "game.ongameover", JToken.FromObject(data)));
        foreach (var viewer in room.Viewers.ToArray())
            await _webSocket!.Send(viewer.Ws, new(WsMessage.MessageType.Event, "game.ongameover", JToken.FromObject(data)));

        room.End();

        await _webSocket!.Send(room.Owner.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, room.Owner) })));
        if (room.Opponent is not null)
            await _webSocket!.Send(room.Opponent.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, room.Opponent) })));
        foreach (var viewer in room.Viewers.ToArray())
            await _webSocket!.Send(viewer.Ws, new(WsMessage.MessageType.Event, "room.onstatechange", JToken.FromObject(new { room = new RoomPublic(room, viewer) })));

        return (true, "");
    }

    public Room? GetJoinedRoom(Player player)
        => _rooms.FirstOrDefault(r => r.Owner == player || r.Opponent == player || r.Viewers.Contains(player));

    public async Task OnPlayerLeft(Player player)
    {
        var tasks = new List<Task>();

        lock (_rooms)
        {
            var rooms = _rooms.Where(r => r.Owner == player).ToArray();
            foreach (var room in rooms)
                tasks.Add(DestroyRoom(room));

            tasks.Add(LeaveRoom(player));

            lock (_updateSubscribers)
                if (_updateSubscribers.Contains(player))
                    _updateSubscribers.Remove(player);
        }

        await Task.WhenAll(tasks);
    }

    public void SubscribeToUpdates(Player player)
    {
        lock (_updateSubscribers)
            if (!_updateSubscribers.Contains(player))
                _updateSubscribers.Add(player);
    }
}
