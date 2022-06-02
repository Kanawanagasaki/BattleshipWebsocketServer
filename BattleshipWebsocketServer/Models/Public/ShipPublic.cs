namespace BattleshipWebsocketServer.Models.Public;

public class ShipPublic
{
    public int x;
    public int y;
    public int size;
    public bool isVertical;
    public bool isDead;

    public ShipPublic(Ship ship)
    {
        x = ship.X;
        y = ship.Y;
        size = ship.Size;
        isVertical = ship.IsVertical;
        isDead = ship.IsDead;
    }
}
