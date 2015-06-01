using System;
using Xamarin.Forms;
using System.Linq;
using System.Collections.Generic;

namespace BugSweeper
{
    class Board : AbsoluteLayout
    {
        // Alternative sizes make the tiles a tad small.
        const int COLS = 9;         // 16
        const int ROWS = 9;         // 16
        const int BUGS = 10;        // 40

        int flaggedTileCount;
        bool isGameInProgress;              // on first tap
        bool isGameInitialized;             // on first double-tap
        bool isGameEnded;

        private IList<Tile> Tiles { get; } = new List<Tile>();

        // Events to notify page.
        public event EventHandler GameStarted;
        public event EventHandler<bool> GameEnded;

        public Board()
        {
            for (int row = 0; row < ROWS; row++)
                for (int col = 0; col < COLS; col++)
                {
                    Tile tile = new Tile(row, col);
                    tile.TileStatusChanged += OnTileStatusChanged;
                    this.Children.Add(tile);
                    Tiles.Add( tile);
                }

            SizeChanged += (sender, args) =>
                {
                    double tileWidth = this.Width / COLS;
                    double tileHeight = this.Height / ROWS;
                    foreach (Tile tile in Tiles)
                    {
                        Rectangle bounds = new Rectangle(tile.Col * tileWidth,
                                                         tile.Row * tileHeight,
                                                         tileWidth, tileHeight);
                        AbsoluteLayout.SetLayoutBounds(tile, bounds);
                    }
                };
            NewGameInitialize();
        }

        private Tile Tile(int row, int col) => Tiles.Single(t => t.Row == row && t.Col == col);

        public void NewGameInitialize()
        {
            // Clear all the tiles.
            foreach (Tile tile in Tiles)
                tile.Initialize();
            isGameInProgress = false;
            isGameInitialized = false;
            isGameEnded = false;
            this.FlaggedTileCount = 0;
        }

        public int FlaggedTileCount
        {
            set
            {
                if (flaggedTileCount != value)
                {
                    flaggedTileCount = value;
                    OnPropertyChanged();
                }
            }
            get
            {
                return flaggedTileCount;
            }
        }

        public int BugCount => BUGS;

        // Not called until the first tile is double-tapped.
        void DefineNewBoard(int tappedRow, int tappedCol)
        {
            var sourceTile = Tile(tappedRow, tappedCol);
            // Begin the assignment of bugs.
            Random random = new Random();
            int bugCount = 0;
            while (bugCount < BUGS)
            {
                // Get random row and column.
                int row = random.Next(ROWS);
                int col = random.Next(COLS);
                var targetTile = Tile(row, col);
                if (targetTile.IsSame(sourceTile) || targetTile.IsBug || targetTile.IsNeibourOf(sourceTile))
                {
                    continue;
                }

                // It's a bug!
                Tile(row, col).IsBug = true;
                // Calculate the surrounding bug count.
                CycleThroughNeighbors(row, col, AddBugToTile);
                bugCount++;
            }
        }

        void CycleThroughNeighbors(int row, int col, Action<int, int> callback)
        {
            var neighbors =  from r in GetNeibourIndicesRang( row, ROWS)
                             from c in GetNeibourIndicesRang( col, COLS)
                             where r != row || c != col
                             select Tile(r,c);

            foreach(var neighbor in neighbors)
            {
                callback(neighbor.Row, neighbor.Col);
            }
        }

        private static IEnumerable<int> GetNeibourIndicesRang(int center, int maxIndex)
        {
            int min = Math.Max(0, center - 1);
            int max = Math.Min(maxIndex - 1, center + 1);
            int count = max - min;
            return Enumerable.Range(min, count);
        }

        void OnTileStatusChanged(object sender, TileStatus tileStatus)
        {
            if (isGameEnded)
                return;

            // With a first tile tapped, the game is now in progress.
            if (!isGameInProgress)
            {
                isGameInProgress = true;

                // Fire the GameStarted event.
                if (GameStarted != null)
                {
                    GameStarted(this, EventArgs.Empty);
                }
            }

            // Update the "flagged" bug count before checking for a loss.
            this.FlaggedTileCount = Tiles.Where(t => t.Status == TileStatus.Flagged).Count();

            // Get the tile whose status has changed.
            Tile changedTile = (Tile)sender;

            // If it's exposed, some actions are required.
            if (tileStatus == TileStatus.Exposed)
            {
                if (!isGameInitialized)
                {
                    DefineNewBoard(changedTile.Row, changedTile.Col);
                    isGameInitialized = true;
                }

                if (changedTile.IsBug)
                {
                    isGameInProgress = false;
                    isGameEnded = true;

                    // Fire the GameEnded event!
                    if (GameEnded != null)
                    {
                        GameEnded(this, false);
                    }
                    return;
                }

                // Auto expose for zero surrounding bugs.
                if (changedTile.SurroundingBugCount == 0)
                {
                    CycleThroughNeighbors(changedTile.Row, changedTile.Col, ExposeTile);
                }
            }

            // If there's a win, celebrate!
            if (HasWon)
            {
                isGameInProgress = false;
                isGameEnded = true;

                // Fire the GameEnded event!
                if (GameEnded != null)
                {
                    GameEnded(this, true);
                }
            }
        }

        private bool HasWon => Tiles.All(CorrectlyChecked);

        private void ExposeTile(int row, int col) => Tile(row, col).Expose();

        private void AddBugToTile(int row, int col) => Tile(row, col).IncreaseBugCount();

        private static bool CorrectlyChecked(Tile tile) =>
                   (tile.IsBug && tile.Status == TileStatus.Flagged) ||
                   (!tile.IsBug && tile.Status == TileStatus.Exposed);
    }
}
