namespace BattleshipWebsocketServer.Models.Public;

public class RoomPublic
{
    public int id;
    public BoardPublic owner;
    public BoardPublic? opponent;
    public string state;
    public bool isOwnerTurn;
    public PlayerPublic[] viewers;

    public RoomPublic(Room room, Player forPlayer)
    {
        id = room.Id;
        owner = new(room.OwnerBoard, room.Owner != forPlayer);
        opponent = room.OpponentBoard is null ? null : new(room.OpponentBoard, room.Opponent != forPlayer);
        state = room.State.ToString().ToLower();
        isOwnerTurn = room.IsOwnerTurn;
        viewers = room.Viewers.Select(v => new PlayerPublic(v)).ToArray();
    }
}
