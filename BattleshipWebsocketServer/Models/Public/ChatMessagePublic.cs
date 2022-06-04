namespace BattleshipWebsocketServer.Models.Public;

public class ChatMessagePublic
{
    public PlayerPublic player;
    public string message;
    public DateTime datetime;

    public ChatMessagePublic(ChatMessage chatMessage)
    {
        player = new PlayerPublic(chatMessage.Player);
        message = chatMessage.Message;
        datetime = chatMessage.DateTime;
    }
}
