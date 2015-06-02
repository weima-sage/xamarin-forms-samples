using System;
using Xamarin.Forms;
using System.Linq;
using System.Collections.Generic;
using static System.Diagnostics.Debug;

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
                var neighbors = GetNeighbors(row, col);
                WriteLine($"{row}, {col} is bug, neighbors are");
                foreach(var neighbor in neighbors){
                    WriteLine($"neibour of {row},{col}: {neighbor.Row}, {neighbor.Col}");
                }

                // Calculate the surrounding bug count.
                CycleThroughNeighbors(row, col, AddBugCount);
                // CycleThroughNeighbors(row, col,
                //     (neighborRow, neighborCol) =>
                //     {
                //         ++(Tile(neighborRow, neighborCol).SurroundingBugCount);
                //     });
                bugCount++;
            }
        }

        void CycleThroughNeighbors(int row, int col, Action<int, int> callback)
        {
            foreach(var neighbor in GetNeighbors(row, col))
            {
                callback(neighbor.Row, neighbor.Col);
            }
        }

        private IEnumerable<Tile> GetNeighbors(int row, int col) =>
            Tiles.Where(t => t.IsNeibourOf(Tile(row, col)));

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
            this.FlaggedTileCount = Tiles.Count(t => t.IsFlagged);

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

        private bool HasWon => Tiles.All( t => t.CorrectlyChecked);

        private void ExposeTile(int row, int col) => Tile(row, col).Expose();

        private void AddBugCount(int row, int col) => Tile(row, col).IncreaseBugCount();
    }
}
