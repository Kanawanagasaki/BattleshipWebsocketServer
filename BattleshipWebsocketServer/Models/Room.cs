namespace BattleshipWebsocketServer.Models;

public class Room
{
    private static int ID_INC = 0;

    public int Id { get; set; }

    public Player Owner { get; private set; }
    public Board OwnerBoard { get; private set; }

    public Player? Opponent { get; private set; }
    public Board? OpponentBoard { get; private set; }

    public States State { get; private set; } = States.Idle;
    public bool IsOwnerTurn { get; private set; } = true;

    private List<Player> _viewers = new();
    public IReadOnlyList<Player> Viewers => _viewers;

    public List<ChatMessage> Messages = new();

    public Room(Player owner)
    {
        Id = ++ID_INC;
        Owner = owner;
        OwnerBoard = new Board(owner);
    }

    public void Join(Player player)
    {
        lock (_viewers)
        {
            if (_viewers.Contains(player))
                return;
            _viewers.Add(player);
        }
    }

    public void Leave(Player player)
    {
        if (Opponent == player)
        {
            OwnerBoard.Reset();
            Opponent = null;
            OpponentBoard = null;
            State = States.Idle;
        }
        if (_viewers.Contains(player))
        {
            _viewers.Remove(player);
        }
    }

    public void Challenge(Player player)
    {
        OwnerBoard.Reset();

        Opponent = player;
        OpponentBoard = new(player);
        State = States.Preparation;
        IsOwnerTurn = Random.Shared.NextDouble() > 0.5d;
        if (_viewers.Contains(player))
        {
            _viewers.Remove(player);
        }
    }

    public void Activate()
    {
        State = States.Active;
    }

    public void End()
    {
        State = States.End;
    }

    public void OnPlayerLeft(Player player)
    {
        if (Opponent == player)
        {
            Opponent = null;
            OpponentBoard = null;
        }
        lock (_viewers)
            if (_viewers.Contains(player))
                _viewers.Remove(player);
    }

    public void AddMessage(ChatMessage message)
    {
        lock (Messages)
        {
            Messages.Add(message);
            if (Messages.Count > 20)
                Messages.RemoveAt(0);
        }
    }

    public void ToggleTurn()
    {
        IsOwnerTurn = !IsOwnerTurn;
    }

    public enum States
    {
        Idle, Preparation, Active, End
    }
}
