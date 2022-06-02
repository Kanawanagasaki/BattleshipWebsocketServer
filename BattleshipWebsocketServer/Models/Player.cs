using System.Net.WebSockets;

namespace BattleshipWebsocketServer.Models;

public class Player
{
    private static int ID_INC = 0;

    internal WebSocket Ws { get; private set; }
    
    public int Id { get; private set; }
    public string Nickname { get; private set; }

    public Player(WebSocket ws, string nickname)
    {
        Id = ++ID_INC;
        Ws = ws;
        Nickname = nickname;
    }
}
