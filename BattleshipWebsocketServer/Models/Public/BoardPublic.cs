namespace BattleshipWebsocketServer.Models.Public;

public class BoardPublic
{
    public int[][] board = new int[Board.Height][];
    public ShipPublic[] ships;
    public PlayerPublic player;
    public bool isReady;

    public string comment = @"numbers in board are: 0 is empty cell, 1 is mark (you shoot and miss), 2 is ship, e is hit (ship hit but not dead), 4 is shipwreck (sunken ship)";

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

        ships = b.Ships.Select(s => new ShipPublic(s)).ToArray();

        player = new(b.Player);
        isReady = b.IsReady;
    }
}
