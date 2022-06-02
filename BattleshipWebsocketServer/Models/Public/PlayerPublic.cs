namespace BattleshipWebsocketServer.Models.Public;

public class PlayerPublic
{
    public int id;
    public string nickname;

    public PlayerPublic(Player player)
    {
        id = player.Id;
        nickname = player.Nickname;
    }
}
