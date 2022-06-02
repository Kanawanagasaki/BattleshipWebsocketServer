using BattleshipWebsocketServer.Models;
using System.Net.WebSockets;

namespace BattleshipWebsocketServer.Services;

public class PlayersService
{
    private Dictionary<WebSocket, Player> _players = new();
    public IReadOnlyDictionary<WebSocket, Player> Players => _players;

    private RoomsService _rooms;

    public PlayersService(RoomsService rooms)
        => (_rooms) = (rooms);

    public (bool success, string message, Player? player) Register(WebSocket ws, string nickname)
    {
        lock(_players)
        {
            if (nickname.Length < 2)
                return (false, "Nickname must be at least 2 characters", null);
            if (nickname.Length > 32)
                return (false, "Nickname must contain no more than 32 characters", null);
            if (!nickname.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
                return (false, "Nickname must be letters, digits, _ or -", null);
            if (_players.Values.Any(pl => pl.Nickname == nickname))
                return (false, "Nickname is already taken", null);
            if (_players.ContainsKey(ws))
                return (true, "You are already logged in", _players[ws]);

            var player = new Player(ws, nickname);
            _players[ws] = player;
            return (true, "", player);
        }
    }

    public async Task<(bool success, string message)> Logout(WebSocket ws)
    {
        Task task;

        lock(_players)
        {
            if (!_players.ContainsKey(ws)) return (false, "You not logged in");
            Console.WriteLine(_players[ws].Nickname + " left");
            task = _rooms.OnPlayerLeft(_players[ws]);
            _players.Remove(ws);
        }

        await task;

        return (true, "");
    }
}
