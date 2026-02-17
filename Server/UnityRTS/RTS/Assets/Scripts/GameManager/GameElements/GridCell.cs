using UnityEngine;
using UnityEngine.Tilemaps;
using GameManager.Graph;

namespace GameManager.GameElements
{
	internal class GridCell : IColorable, IBuildable, IPositionable
	{
		internal Vector3Int Position { get; set; }

		internal Tilemap TileMap { get; set; }

		internal TileBase Tile { get; set; }

		private bool isBuildable;

		#region Interface Implementations
		public void ChangeColor(Color color)
		{
		}

		public bool IsBuildable()
		{
			return this.isBuildable;
		}

		public void SetBuildable(bool isBuildable)
		{
			//if (!isBuildable)
			//{ TileMap.SetColor(Position, Color.black); }
			//else
			//{ TileMap.SetColor(Position, Color.white); }

			this.isBuildable = isBuildable;
		}

		public Vector3 GetPosition()
		{
			return Position;
		}

		#endregion

		internal GridCell(Tilemap tileMap, Vector3Int position)
		{
			this.TileMap = tileMap;
			this.Position = position;
			this.Tile = tileMap.GetTile(Position);
			this.isBuildable = true;
		}
	}
}
