namespace BattleshipWebsocketServer.Models;

public class Ship
{
    public int X { get; set; }
    public int Y { get; set; }
    public bool IsVertical { get; set; }
    public int Size { get; set; }
    public bool IsDead { get; set; } = false;
}
