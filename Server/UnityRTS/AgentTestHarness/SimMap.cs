using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Grid-based map with A* pathfinding matching the real game's MapManager.
    /// Coordinate system: (0,0) at bottom-left, +x right, +y up.
    /// Building footprints extend from anchor (x,y) rightward and downward:
    ///   cells = (x+i, y-j) for i in [0..sizeX), j in [0..sizeY).
    /// </summary>
    public class SimMap
    {
        private readonly bool[,] buildable;
        private readonly bool[,] walkable;

        public int Width { get; }
        public int Height { get; }
        public Position Size => new Position(Width, Height);

        public SimMap(int width, int height)
        {
            Width = width;
            Height = height;
            buildable = new bool[width, height];
            walkable = new bool[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    buildable[x, y] = true;
                    walkable[x, y] = true;
                }
            }
        }

        #region Cell Queries

        public bool IsPositionValid(Position p)
        {
            return p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;
        }

        public bool IsPositionBuildable(Position p)
        {
            return IsPositionValid(p) && buildable[p.X, p.Y];
        }

        public bool IsPositionWalkable(Position p)
        {
            return IsPositionValid(p) && walkable[p.X, p.Y];
        }

        /// <summary>
        /// Check if the full footprint for a unit type is buildable at the given anchor.
        /// Footprint: (anchor.X + i, anchor.Y - j) for i in [0..sizeX), j in [0..sizeY).
        /// </summary>
        public bool IsAreaBuildable(UnitType unitType, Position anchor)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y - j);
                    if (!IsPositionValid(cell) || !buildable[cell.X, cell.Y])
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check if the unit footprint plus a 1-cell border is all buildable.
        /// Matches the real game's IsBoundedAreaBuildable.
        /// </summary>
        public bool IsBoundedAreaBuildable(UnitType unitType, Position anchor)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            for (int i = -1; i <= size.X; i++)
            {
                for (int j = -1; j <= size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y - j);
                    if (!IsPositionValid(cell) || !buildable[cell.X, cell.Y])
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region Cell Modification

        /// <summary>
        /// Set buildability/walkability for the footprint of a unit.
        /// Mobile units (CanMove) keep cells walkable when placed (matching real game).
        /// </summary>
        public void SetAreaBuildability(UnitType unitType, Position anchor, bool isBuildable)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            bool canMove = GameConstants.CAN_MOVE[unitType];

            for (int i = 0; i < size.X; i++)
            {
                for (int j = 0; j < size.Y; j++)
                {
                    var cell = new Position(anchor.X + i, anchor.Y - j);
                    if (IsPositionValid(cell))
                    {
                        buildable[cell.X, cell.Y] = isBuildable;
                        // Mobile units don't block pathfinding
                        if (isBuildable || !canMove)
                            walkable[cell.X, cell.Y] = isBuildable;
                    }
                }
            }
        }

        /// <summary>Mark a single cell as unbuildable/unwalkable (for walls).</summary>
        public void SetCellBlocked(Position p)
        {
            if (IsPositionValid(p))
            {
                buildable[p.X, p.Y] = false;
                walkable[p.X, p.Y] = false;
            }
        }

        #endregion

        #region Neighbor Queries

        /// <summary>
        /// Get all grid positions in the ring around a unit's footprint.
        /// Matches the real game's GetGridPositionsNearUnit.
        /// </summary>
        public List<Position> GetPositionsNearUnit(UnitType unitType, Position anchor)
        {
            var size = GameConstants.UNIT_SIZE[unitType];
            var positions = new List<Position>();

            // Top and bottom rows
            for (int i = anchor.X - 1; i <= anchor.X + size.X; i++)
            {
                var top = new Position(i, anchor.Y + 1);
                if (IsPositionValid(top)) positions.Add(top);

                var bottom = new Position(i, anchor.Y - size.Y);
                if (IsPositionValid(bottom)) positions.Add(bottom);
            }

            // Left and right columns (excluding corners already added)
            for (int j = anchor.Y - size.Y + 1; j <= anchor.Y; j++)
            {
                var left = new Position(anchor.X - 1, j);
                if (IsPositionValid(left)) positions.Add(left);

                var right = new Position(anchor.X + size.X, j);
                if (IsPositionValid(right)) positions.Add(right);
            }

            return positions;
        }

        /// <summary>
        /// Get all buildable positions in the ring around a unit's footprint.
        /// </summary>
        public List<Position> GetBuildablePositionsNearUnit(UnitType unitType, Position anchor)
        {
            return GetPositionsNearUnit(unitType, anchor)
                .Where(p => buildable[p.X, p.Y])
                .ToList();
        }

        /// <summary>
        /// Find all valid build positions on the map for a given unit type.
        /// Matches the real game's FindProspectiveBuildPositions (checks bounded area).
        /// </summary>
        public List<Position> FindProspectiveBuildPositions(UnitType unitType)
        {
            var result = new List<Position>();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var pos = new Position(x, y);
                    if (IsBoundedAreaBuildable(unitType, pos))
                        result.Add(pos);
                }
            }
            return result;
        }

        #endregion

        #region A* Pathfinding

        /// <summary>
        /// Find shortest path between two positions using A* with Euclidean heuristic.
        /// Cardinal edges cost 1.0, diagonal edges cost sqrt(2).
        /// Returns empty list if no path exists. The start position is excluded from the result.
        /// </summary>
        public List<Position> FindPath(Position start, Position end)
        {
            if (!IsPositionValid(start) || !IsPositionValid(end))
                return new List<Position>();

            if (start == end)
                return new List<Position>();

            // End must be walkable
            if (!walkable[end.X, end.Y])
                return new List<Position>();

            // Start node is allowed to be unwalkable (unit may be inside a building)

            var openSet = new SortedSet<AStarNode>(new AStarNodeComparer());
            var gScore = new Dictionary<int, float>();
            var cameFrom = new Dictionary<int, int>();
            var inOpen = new Dictionary<int, AStarNode>();

            int startKey = PosToKey(start);
            int endKey = PosToKey(end);

            float startG = 0f;
            float startF = Heuristic(start, end);
            var startNode = new AStarNode(startKey, startG, startF);
            openSet.Add(startNode);
            gScore[startKey] = 0f;
            inOpen[startKey] = startNode;

            while (openSet.Count > 0)
            {
                var current = openSet.Min;
                openSet.Remove(current);
                inOpen.Remove(current.Key);

                if (current.Key == endKey)
                    return ReconstructPath(cameFrom, endKey);

                var currentPos = KeyToPos(current.Key);

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        int nx = currentPos.X + dx;
                        int ny = currentPos.Y + dy;

                        if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                            continue;

                        if (!walkable[nx, ny])
                            continue;

                        int neighborKey = nx * Height + ny;
                        float edgeCost = (dx != 0 && dy != 0) ? 1.41421356f : 1.0f;
                        float tentativeG = gScore[current.Key] + edgeCost;

                        if (gScore.TryGetValue(neighborKey, out float existingG) && tentativeG >= existingG)
                            continue;

                        gScore[neighborKey] = tentativeG;
                        cameFrom[neighborKey] = current.Key;

                        var neighborPos = new Position(nx, ny);
                        float f = tentativeG + Heuristic(neighborPos, end);

                        // Remove old entry if exists
                        if (inOpen.TryGetValue(neighborKey, out var oldNode))
                        {
                            openSet.Remove(oldNode);
                            inOpen.Remove(neighborKey);
                        }

                        var newNode = new AStarNode(neighborKey, tentativeG, f);
                        openSet.Add(newNode);
                        inOpen[neighborKey] = newNode;
                    }
                }
            }

            return new List<Position>();
        }

        /// <summary>
        /// Find a path from start to any walkable cell adjacent to the given unit.
        /// Tries each buildable neighbor; returns the first successful path.
        /// </summary>
        public List<Position> FindPathToUnit(Position start, UnitType unitType, Position unitAnchor)
        {
            var neighbors = GetBuildablePositionsNearUnit(unitType, unitAnchor);
            foreach (var neighbor in neighbors)
            {
                var path = FindPath(start, neighbor);
                if (path.Count > 0)
                    return path;
            }
            return new List<Position>();
        }

        private float Heuristic(Position a, Position b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private int PosToKey(Position p) => p.X * Height + p.Y;

        private Position KeyToPos(int key) => new Position(key / Height, key % Height);

        private List<Position> ReconstructPath(Dictionary<int, int> cameFrom, int endKey)
        {
            var path = new List<Position>();
            int current = endKey;
            while (cameFrom.ContainsKey(current))
            {
                path.Add(KeyToPos(current));
                current = cameFrom[current];
            }
            path.Reverse();
            return path;
        }

        /// <summary>Node used in the A* open set.</summary>
        private struct AStarNode
        {
            public readonly int Key;
            public readonly float G;
            public readonly float F;

            public AStarNode(int key, float g, float f)
            {
                Key = key;
                G = g;
                F = f;
            }
        }

        /// <summary>
        /// Comparer for A* nodes: sort by F, break ties by G (prefer higher G = closer to goal),
        /// then by key for deterministic ordering.
        /// </summary>
        private class AStarNodeComparer : IComparer<AStarNode>
        {
            public int Compare(AStarNode a, AStarNode b)
            {
                int cmp = a.F.CompareTo(b.F);
                if (cmp != 0) return cmp;
                // Break ties: prefer higher G (closer to goal)
                cmp = b.G.CompareTo(a.G);
                if (cmp != 0) return cmp;
                return a.Key.CompareTo(b.Key);
            }
        }

        #endregion
    }
}
