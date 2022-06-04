namespace BattleshipWebsocketServer.Models;

public class ChatMessage
{
    public Player Player;
    public string Message;
    public DateTime DateTime;

    public ChatMessage(Player player, string message)
    {
        Player = player;
        Message = message;
        DateTime = DateTime.UtcNow;
    }
}
