namespace BattleshipWebsocketServer.Models.Public;

public class BoardPublic
{
    public int[][] board = new int[Board.Height][];
    public ShipPublic[] ships;
    public PlayerPublic player;
    public bool isReady;

    public string comment = @"numbers in board are: 0 is empty cell, 1 is mark (you shoot and miss), 2 is ship, 3 is hit (ship hit but not dead), 4 is shipwreck (sunken ship), 5+ is tagged ships";

    public BoardPublic(Board b, bool hide)
    {
        for (int iy = 0; iy < Board.Height; iy++)
        {
            board[iy] = new int[Board.Width];

            for (int ix = 0; ix < Board.Width; ix++)
            {
                if (hide)
                    board[iy][ix] = (int)(b.Cells[ix, iy] == Board.BoardCell.Ship ? Board.BoardCell.Empty : b.Cells[ix, iy]);
                else board[iy][ix] = (int)b.Cells[ix, iy];
            }
        }

        foreach(var ship in b.Ships)
        {
            if (hide && !ship.IsDead) continue;
            if (ship.Tag < 5) continue;

            int ex = ship.X + (ship.IsVertical ? 0 : ship.Size - 1);
            int ey = ship.Y + (ship.IsVertical ? ship.Size - 1 : 0);

            for(int ix = ship.X; ix <= ex; ix++)
                for(int iy = ship.Y; iy <= ey; iy++)
                    board[iy][ix] = ship.Tag;
        }

        if(hide)
            ships = b.Ships.Where(s => s.IsDead).Select(s => new ShipPublic(s)).ToArray();
        else ships = b.Ships.Select(s => new ShipPublic(s)).ToArray();

        player = new(b.Player);
        isReady = b.IsReady;
    }
}
