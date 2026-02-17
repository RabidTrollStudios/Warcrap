using System;
using GameManager.EnumTypes;
using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// Class with helper functions to convert between coordinate systems
	/// </summary>
	public class Utility
	{
		/// <summary>
		/// Convert a Vector3 to a Vector3Int in the grid
		/// </summary>
		/// <param name="position"></param>
		/// <returns></returns>
		public static Vector3Int WorldToGrid(Vector3 position)
		{
			return new Vector3Int((int)position.x, (int)position.y, 0);
		}

		/// <summary>
		/// Convert a Vector3Int in the grid to a vector2
		/// </summary>
		/// <param name="gridPosition"></param>
		/// <returns></returns>
		public static Vector3 GridToWorld(Vector3Int gridPosition)
		{
			return new Vector3(gridPosition.x, gridPosition.y, 0);

		}

		/// <summary>
		/// Convert a gridcell's number to a Vector3Int in the grid
		/// </summary>
		/// <param name="gridNbr"></param>
		/// <returns></returns>
		public static Vector3Int IntToGrid(int gridNbr)
		{
			return new Vector3Int(gridNbr / GameManager.Instance.Map.MapSize.y, gridNbr % GameManager.Instance.Map.MapSize.y, 0);
		}

		/// <summary>
		/// Convert a Vector3Int in the grid to the gridcell's number
		/// </summary>
		/// <param name="position"></param>
		/// <returns></returns>
		public static int GridToInt(Vector3Int position)
		{
			return position.x * GameManager.Instance.Map.MapSize.y + position.y;
		}

		/// <summary>
		/// Safely normalize a vector2, preventing divide
		/// by zero errors or infinitely large vectors
		/// </summary>
		/// <param name="vector"></param>
		/// <returns></returns>
		public static Vector3 SafeNormalize(Vector3 vector)
		{
			Vector3 output = Vector3.zero;
			if (Math.Abs(vector.x) > 0.0f && Math.Abs(vector.y) > 0.0f)
			{
				output = vector.normalized;
			}
			else if (Math.Abs(vector.y) > 0.0f)
			{
				output.y = Math.Sign(vector.y);
			}
			else if (Math.Abs(vector.x) > 0.0f)
			{
				output.x = Math.Sign(vector.x);
			}
			return output;
		}

		/// <summary>
		/// Is the Vector3Int a valid position in the world
		/// </summary>
		/// <param name="gridPosition">position to test</param>
		/// <returns>true if in the world</returns>
		public static bool IsValidGridLocation(Vector3Int gridPosition)
		{
			return !(gridPosition.x < 0 || gridPosition.x >= GameManager.Instance.Map.MapSize.x
					|| gridPosition.y < 0 || gridPosition.y >= GameManager.Instance.Map.MapSize.y);
		}

		/// <summary>
		/// Used by the animation system to convert between the actual direction the agent is moving
		/// and the direction of its animation
		/// </summary>
		/// <param name="startPosition">starting grid position</param>
		/// <param name="endPosition">ending grid position</param>
		/// <returns></returns>
		public static Direction ConvertPositionToDirection(Vector3Int startPosition, Vector3Int endPosition)
		{
			Vector3Int direction = endPosition - startPosition;
			if (direction.x == 0 && direction.y == -1)
			{
				return Direction.S;
			}
			else if (direction.x == 1 && direction.y == -1)
			{
				return Direction.SE;
			}
			else if (direction.x == 1 && direction.y == 0)
			{
				return Direction.E;
			}
			else if (direction.x == 1 && direction.y == 1)
			{
				return Direction.NE;
			}
			else if (direction.x == 0 && direction.y == 1)
			{
				return Direction.N;
			}
			else if (direction.x == -1 && direction.y == 1)
			{
				return Direction.NW;
			}
			else if (direction.x == -1 && direction.y == 0)
			{
				return Direction.W;
			}
			else //if (direction.x == -1 && direction.y == -1)
			{
				return Direction.SW;
			}
		}
	}
}
