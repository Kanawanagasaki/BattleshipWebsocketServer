namespace BattleshipWebsocketServer.Models.Public;

public class RoomPublic
{
    public int id;
    public BoardPublic owner;
    public BoardPublic? opponent;
    public string state;
    public bool isOwnerTurn;
    public PlayerPublic[] viewers;
    public ChatMessagePublic[] messages;

    public RoomPublic(Room room, Player forPlayer)
    {
        id = room.Id;
        owner = new(room.OwnerBoard, room.Owner != forPlayer && room.State != Room.States.End);
        opponent = room.OpponentBoard is null ? null : new(room.OpponentBoard, room.Opponent != forPlayer && room.State != Room.States.End);
        state = room.State.ToString().ToLower();
        isOwnerTurn = room.IsOwnerTurn;
        viewers = room.Viewers.Select(v => new PlayerPublic(v)).ToArray();
        messages = room.Messages.Select(m => new ChatMessagePublic(m)).ToArray();
    }
}
