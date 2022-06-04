namespace BattleshipWebsocketServer.Models;

public class Board
{
    public const int Width = 10;
    public const int Height = 10;
    public static int[] Sizes = new int[] { 5, 4, 3, 3, 2 };

    public BoardCell[,] Cells { get; private set; }
    public Player Player { get; private set; }

    public Ship[] Ships { get; private set; } = Array.Empty<Ship>();

    public bool IsReady { get; private set; } = false;

    public Board(Player player)
    {
        Player = player;

        Cells = new BoardCell[Width, Height];
        for (int iy = 0; iy < Height; iy++)
            for (int ix = 0; ix < Width; ix++)
                Cells[ix, iy] = BoardCell.Empty;
    }

    public bool CheckShipSizes(Ship[] ships)
    {
        List<int> sizes = new();
        sizes.AddRange(Sizes);
        foreach (var ship in ships)
        {
            if (!sizes.Contains(ship.Size)) return false;
            sizes.Remove(ship.Size);
        }
        return sizes.Count == 0;
    }

    public bool TryPlaceShips(Ship[] ships)
    {
        var board = new bool[Width, Height];
        foreach (var ship in ships)
        {
            int x1 = ship.X;
            int y1 = ship.Y;
            int x2 = ship.X + (ship.IsVertical ? 0 : ship.Size - 1);
            int y2 = ship.Y + (ship.IsVertical ? ship.Size - 1 : 0);

            if (x1 < 0 || x2 >= Width || y1 < 0 || y2 >= Height) return false;

            for (int iy = y1; iy <= y2; iy++)
            {
                for (int ix = x1; ix <= x2; ix++)
                {
                    if (iy < 0 || iy >= Height || ix < 0 || ix >= Width)
                        continue;
                    if (board[ix, iy])
                        return false;
                    if(x1 <= ix && ix <= x2 && y1 <= iy && iy <= y2)
                        board[ix, iy] = true;
                }
            }
        }

        for (int iy = 0; iy < Height; iy++)
            for (int ix = 0; ix < Width; ix++)
                Cells[iy, ix] = BoardCell.Empty;

        foreach (var ship in ships)
        {
            int x1 = ship.X;
            int y1 = ship.Y;
            int x2 = ship.X + (ship.IsVertical ? 0 : ship.Size - 1);
            int y2 = ship.Y + (ship.IsVertical ? ship.Size - 1 : 0);

            for (int iy = y1; iy <= y2; iy++)
                for (int ix = x1; ix <= x2; ix++)
                    Cells[ix, iy] = BoardCell.Ship;
        }

        Ships = ships;

        IsReady = true;

        return true;
    }

    public void Reset()
    {
        for (int iy = 0; iy < Height; iy++)
            for (int ix = 0; ix < Width; ix++)
                Cells[iy, ix] = BoardCell.Empty;

        Ships = Array.Empty<Ship>();

        IsReady = false;
    }

    public (bool success, bool isHit, Ship? sunkenShip, string message) Salvo(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return (false, false, null, "Coordinates was outside the border");
        if (Cells[x, y] == BoardCell.Hit || Cells[x, y] == BoardCell.Shipwreck || Cells[x, y] == BoardCell.Mark)
            return (false, false, null, "You already shot in this coordinates");
        if (Cells[x, y] == BoardCell.Empty)
        {
            Cells[x, y] = BoardCell.Mark;
            return (true, false, null, "");
        }
        foreach (var ship in Ships)
        {
            int x1 = ship.X;
            int y1 = ship.Y;
            int x2 = ship.X + (ship.IsVertical ? 0 : ship.Size - 1);
            int y2 = ship.Y + (ship.IsVertical ? ship.Size - 1 : 0);

            if (x1 <= x && x <= x2 && y1 <= y && y <= y2)
            {
                Cells[x, y] = BoardCell.Hit;
                bool dead = true;
                for (int ix = x1; ix <= x2; ix++)
                    for (int iy = y1; iy <= y2; iy++)
                        if (Cells[ix, iy] != BoardCell.Hit)
                            dead = false;

                if (dead)
                {
                    for (int ix = x1; ix <= x2; ix++)
                        for (int iy = y1; iy <= y2; iy++)
                            Cells[ix, iy] = BoardCell.Shipwreck;
                    ship.IsDead = dead;
                }

                return (true, true, ship.IsDead ? ship : null, "");
            }
        }

        return (false, false, null, "Internal error");
    }

    public void Show()
    {
        for(int iy = 0; iy < Height; iy++)
        {
            Console.WriteLine(string.Join("_", Enumerable.Range(0, Width + 1).Select(i => " ")));
            Console.Write("|");
            for (int ix = 0; ix < Width; ix++)
            {
                Console.BackgroundColor = Cells[ix, iy] switch
                {
                    BoardCell.Empty => ConsoleColor.Black,
                    BoardCell.Mark => ConsoleColor.Gray,
                    BoardCell.Ship => ConsoleColor.DarkBlue,
                    BoardCell.Hit => ConsoleColor.DarkYellow,
                    BoardCell.Shipwreck => ConsoleColor.DarkRed,
                    _ => ConsoleColor.Black
                };
                Console.Write(" ");
                Console.ResetColor();
                Console.Write("|");
            }
            Console.WriteLine("|");
        }
    }

    public enum BoardCell : int
    {
        Empty = 0,
        Mark = 1,
        Ship = 2,
        Hit = 3,
        Shipwreck = 4
    }
}
