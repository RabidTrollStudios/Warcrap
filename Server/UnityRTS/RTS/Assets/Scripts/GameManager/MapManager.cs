using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using GameManager.Graph;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GameManager
{
	/// <summary>
	/// Manages the game map, grid cells, pathfinding graph, and buildability queries.
	/// </summary>
	public class MapManager
	{
		/// <summary>
		/// Size of the map, +x is "right", +y is "up", z is ignored
		/// </summary>
		public Vector3Int MapSize { get; private set; }

		/// <summary>
		/// The tilemap that renders the Influence Map on top of the game grid
		/// </summary>
		public Tilemap InfluenceMap { get; set; }

		/// <summary>
		/// 2D array of gridcells the size of the Map
		/// </summary>
		internal GridCell[,] GridCells { get; private set; }

		/// <summary>
		/// Graph used for pathfinding
		/// </summary>
		private Graph<GridCell> Graph { get; set; }

		/// <summary>
		/// Primary tilemap used to define the grid size
		/// </summary>
		private Tilemap mainTilemap;

		/// <summary>
		/// Generate the graph based on the tilemaps
		/// </summary>
		/// <param name="grid">the grid containing all tilemaps</param>
		/// <param name="logContext">GameObject for debug log context</param>
		/// <returns>the generated graph, or null on error</returns>
		internal Graph<GridCell> GenerateGraph(GameObject grid, GameObject logContext)
		{
			Graph = new Graph<GridCell>();

			// Find the largest bounds from all of the tilemaps
			MapSize = Vector3Int.zero;
			foreach (Tilemap tilemap in grid.GetComponentsInChildren<Tilemap>())
			{
				tilemap.CompressBounds();

				if (tilemap.size.x > MapSize.x)
					MapSize = new Vector3Int(tilemap.size.x, MapSize.y, MapSize.z);
				if (tilemap.size.y > MapSize.y)
					MapSize = new Vector3Int(MapSize.x, tilemap.size.y, MapSize.z);
				if (tilemap.size.z > MapSize.z)
					MapSize = new Vector3Int(MapSize.x, MapSize.y, tilemap.size.z);
			}

			// If there are no tilemaps to process, produce an error
			if (grid.GetComponentsInChildren<Tilemap>().Length == 0)
			{
				GameManager.Instance.Log("ERROR: no tilemaps", logContext);
				return null;
			}

			// Use the first tilemap as the map size and locations of tiles
			mainTilemap = grid.GetComponentsInChildren<Tilemap>()[0];

			// Create the nodes
			GridCells = new GridCell[MapSize.x, MapSize.y];
			for (int i = 0; i < MapSize.x; ++i)
			{
				for (int j = 0; j < MapSize.y; ++j)
				{
					Vector3Int position = new Vector3Int(i, j, 0);
					GridCells[i, j] = new GridCell(mainTilemap, position);
					Graph.AddNode(Utility.GridToInt(position), GridCells[i, j]);
				}
			}

			// Build edges from all neighboring tiles
			GenerateEdges();

			// Set all of the unbuildable nodes by iterating through the Tilemaps
			for (int t = 1; t < grid.GetComponentsInChildren<Tilemap>().Length; ++t)
			{
				Tilemap tilemap = grid.GetComponentsInChildren<Tilemap>()[t];

				if (tilemap.CompareTag("InfluenceMap"))
				{
					InfluenceMap = tilemap;
					continue;
				}

				for (int i = 0; i < MapSize.x; ++i)
				{
					for (int j = 0; j < MapSize.y; ++j)
					{
						Vector3Int position = new Vector3Int(i, j, 0);

						TileBase tile = tilemap.GetTile(position);
						if (tile != null)
						{
							GridCells[i, j].SetBuildable(false);
						}
					}
				}
			}
			return Graph;
		}

		/// <summary>
		/// Generate all of the edges of the graph
		/// </summary>
		private void GenerateEdges()
		{
			for (int i = 0; i < MapSize.x; ++i)
			{
				for (int j = 0; j < MapSize.y; ++j)
				{
					for (int m = i - 1; m < i + 2; ++m)
					{
						for (int n = j - 1; n < j + 2; ++n)
						{
							if (m >= 0 && n >= 0 && m < MapSize.x && n < MapSize.y
								&& (i != m || j != n))
							{
								Graph.AddEdge(Utility.GridToInt(new Vector3Int(i, j, 0)),
											  Utility.GridToInt(new Vector3Int(m, n, 0)),
											  Vector3.Distance(GridCells[i, j].Position, GridCells[m, n].Position));
							}
						}
					}
				}
			}

			Graph.CalculateEstimatedCosts();
		}

		/// <summary>
		/// Determines if a specific tile is buildable
		/// </summary>
		public bool IsGridPositionBuildable(Vector3Int position)
		{
			return GridCells[position.x, position.y].IsBuildable();
		}

		/// <summary>
		/// Determines if the unit can be built in that area (based on size of unit)
		/// </summary>
		public bool IsAreaBuildable(UnitType unitType, Vector3Int gridPosition)
		{
			Vector3Int gridPos = Vector3Int.zero;
			Vector3Int size = Constants.UNIT_SIZE[unitType];

			for (int i = 0; i < size.x; ++i)
			{
				for (int j = 0; j < size.y; ++j)
				{
					gridPos = gridPosition + new Vector3Int(i, -j, 0);

					if (!Utility.IsValidGridLocation(gridPos)
						|| !IsGridPositionBuildable(gridPos))
					{
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Determines if the unit can be built in that area with a walkable "boundary" around it.
		/// </summary>
		public bool IsBoundedAreaBuildable(UnitType unitType, Vector3Int gridPosition)
		{
			Vector3Int gridPos = Vector3Int.zero;
			Vector3Int size = Constants.UNIT_SIZE[unitType];

			for (int i = -1; i <= size.x; ++i)
			{
				for (int j = -1; j <= size.y; ++j)
				{
					gridPos = gridPosition + new Vector3Int(i, -j, 0);

					if (!Utility.IsValidGridLocation(gridPos)
						|| !IsGridPositionBuildable(gridPos))
					{
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Determines if the gridPosition is a neighbor of the unit
		/// </summary>
		public bool IsNeighborOfUnit(Vector3Int gridPosition, UnitType unitType, Vector3Int unitGridPosition)
		{
			var neighbors = GetGridPositionsNearUnit(unitType, unitGridPosition);
			return neighbors.Contains(gridPosition);
		}

		/// <summary>
		/// Get all of the grid positions surrounding a particular unit
		/// </summary>
		public List<Vector3Int> GetGridPositionsNearUnit(UnitType unitType, Vector3Int gridPosition)
		{
			Vector3Int gridPos;
			List<Vector3Int> positions = new List<Vector3Int>();

			for (int i = gridPosition.x - 1; i <= gridPosition.x + Constants.UNIT_SIZE[unitType].x; ++i)
			{
				gridPos = new Vector3Int(i, gridPosition.y + 1, 0);
				if (Utility.IsValidGridLocation(gridPos))
					positions.Add(gridPos);

				gridPos = new Vector3Int(i, gridPosition.y - Constants.UNIT_SIZE[unitType].y, 0);
				if (Utility.IsValidGridLocation(gridPos))
					positions.Add(gridPos);
			}

			for (int j = gridPosition.y - Constants.UNIT_SIZE[unitType].y + 1; j <= gridPosition.y; ++j)
			{
				gridPos = new Vector3Int(gridPosition.x - 1, j, 0);
				if (Utility.IsValidGridLocation(gridPos))
					positions.Add(gridPos);

				gridPos = new Vector3Int(gridPosition.x + Constants.UNIT_SIZE[unitType].x, j, 0);
				if (Utility.IsValidGridLocation(gridPos))
					positions.Add(gridPos);
			}

			return positions;
		}

		/// <summary>
		/// Find all of the buildable grid positions near this unit
		/// </summary>
		public List<Vector3Int> GetBuildableGridPositionsNearUnit(UnitType unitType, Vector3Int gridPosition)
		{
			List<Vector3Int> positions = GetGridPositionsNearUnit(unitType, gridPosition);
			return positions.Where(IsGridPositionBuildable).ToList();
		}

		/// <summary>
		/// Find a random location that is buildable for the unit type provided
		/// </summary>
		public Vector3Int GetRandomBuildableLocation(UnitType unitType)
		{
			Vector3Int location = Vector3Int.zero;

			do
			{
				location = new Vector3Int(UnityEngine.Random.Range(1, MapSize.x), UnityEngine.Random.Range(1, MapSize.y), 0);
			} while (!IsAreaBuildable(unitType, location));

			return location;
		}

		/// <summary>
		/// Get the path from a gridPosition to a position near the unit on any side of it
		/// </summary>
		public List<Vector3Int> GetPathToUnit(Vector3Int gridPosition, UnitType unitType, Vector3Int unitGridPosition)
		{
			List<Vector3Int> path = new List<Vector3Int>();
			List<Vector3Int> positions = GetBuildableGridPositionsNearUnit(unitType, unitGridPosition);

			foreach (var position in positions)
			{
				path = GetPathBetweenGridPositions(gridPosition, position);
				if (path.Count > 0)
				{
					return path;
				}
			}
			return path;
		}

		/// <summary>
		/// Gets the path between two grid positions
		/// </summary>
		public List<Vector3Int> GetPathBetweenGridPositions(Vector3Int startGridPosition, Vector3Int endGridPosition)
		{
			List<Vector3Int> path = new List<Vector3Int>();

			int start = Utility.GridToInt(startGridPosition);
			int end = Utility.GridToInt(endGridPosition);

			List<int> pathOfInts = Graph.AStarSearch(start, end);

			foreach (var nodeNbr in pathOfInts)
			{
				path.Add(Utility.IntToGrid(nodeNbr));
			}

			return path;
		}

		/// <summary>
		/// Set the unit's current cell(s) to buildable or not
		/// </summary>
		public void SetAreaBuildability(UnitType unitType, Vector3Int gridPosition, bool isBuildable)
		{
			Vector3Int gridPos = Vector3Int.zero;
			Vector3Int size = Constants.UNIT_SIZE[unitType];

			for (int i = 0; i < size.x; ++i)
			{
				for (int j = 0; j < size.y; ++j)
				{
					gridPos = gridPosition + new Vector3Int(i, -j, 0);

					if (Utility.IsValidGridLocation(gridPos) &&
						IsGridPositionBuildable(gridPos) != isBuildable)
					{
						GridCells[gridPos.x, gridPos.y].SetBuildable(isBuildable);
					}
				}
			}
		}
	}
}
