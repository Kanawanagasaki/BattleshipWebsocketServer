namespace BattleshipWebsocketServer.Models;

public class Ship
{
    public int X { get; set; } = 0;
    public int Y { get; set; } = 0;
    public bool IsVertical { get; set; } = false;
    public int Size { get; set; } = 0;
    public bool IsDead { get; set; } = false;
    public int Tag { get; set; } = 0;
}
