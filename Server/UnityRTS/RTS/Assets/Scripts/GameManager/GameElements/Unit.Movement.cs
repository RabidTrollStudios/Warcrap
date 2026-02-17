using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using GameManager.EnumTypes;
using GameManager.Graph;
using UnityEngine;
using UnityEngine.UI;

namespace GameManager.GameElements
{
	public partial class Unit
	{
		#region Update Methods

		/// <summary>
		/// Update this unit
		/// </summary>
		internal void Update()
		{
			MineUnit = GameManager.Instance.Units.GetUnit(mineUnit);
			BaseUnit = GameManager.Instance.Units.GetUnit(baseUnit);

			pathUpdateCounter++;
			HasDebugging = GameManager.Instance.HasUnitDebugging;

			UpdateDebuggingInfo();

			// If this unit is dead, destroy it
			if (Health <= 0)
			{
				GameManager.Instance.Units.DestroyUnit(gameObject);
			}
			// Otherwise, if this unit is idle
			else if (CurrentAction == UnitAction.IDLE)
			{
				path.Clear();
				TargetGridPos = GridPosition; // TODO
				TargetUnitType = UnitType.WORKER;
				AttackUnit = null;
				MineUnit = null;
				BaseUnit = null;
			}
			else //if (!isWandering)
			{
				// If we were ordered to gather and we can gather
				if (CurrentAction == UnitAction.GATHER && CanGather)
				{
					UpdateGather();
				}
				else if (CurrentAction == UnitAction.ATTACK && CanAttack)
				{
					UpdateAttack();
				}
				else if (CurrentAction == UnitAction.BUILD && CanBuild)
				{
					UpdateBuild();
				}
				else if (CurrentAction == UnitAction.MOVE && CanMove)
				{
					UpdateMove();
				}
				else if (CurrentAction == UnitAction.TRAIN && CanTrain)
				{
					UpdateTrain();
				}
			}
		}

		/// <summary>
		/// Map the current velocity to the direction the unit is moving
		/// South is 0, directions are counter-clockwise
		/// </summary>
		/// <returns></returns>
		private void MapVelocityToDirection()
		{
			if (animator == null)
				return;

			// TODO: Keep working on this to flesh out all the directions, this math seems wrong....
			// If south
			if (Math.Abs(velocity.x - 0) < .1f && Math.Abs(velocity.y - 1) < .1f)
			{
				animator.SetInteger("Direction", 0);
			}
			else if (Math.Abs(velocity.x - 0) > .1f && Math.Abs(velocity.y - 1) < .1f)
			{
				animator.SetInteger("Direction", 0);
			}

		}

		internal void FixedUpdate()
		{
			// If we have a path, move along it
			if (path.Count > 0)
			{
				// If the next cell in the path is buildable (truly empty), move forward
				Vector3Int nextTarget = path[0];
				if (GameManager.Instance.Map.IsGridPositionBuildable(nextTarget))
				{
					localAvoidWaitFrames = 0;

					// Calculate our velocity toward our target and move along it
					velocity = nextTarget - WorldPosition;
					velocity = Utility.SafeNormalize(velocity);

					// Determine how far we are from our current target
					float distToTarget =
						Vector3.Distance(nextTarget, WorldPosition);

					// If we're close to our target but we're in the middle of the path
					// Move to the target and then move toward the next point
					if (distToTarget <= Speed)
					{
						GameManager.Instance.Map.SetAreaBuildability(gameObject.GetComponent<Unit>().UnitType, nextTarget, false);
						GameManager.Instance.Map.SetAreaBuildability(gameObject.GetComponent<Unit>().UnitType, GridPosition, true);
						GridPosition = nextTarget;
						WorldPosition = nextTarget;
						path.RemoveAt(0);
						if (path.Count > 0)
						{
							nextTarget = path[0];
							velocity = Utility.SafeNormalize(nextTarget - WorldPosition);
							WorldPosition += velocity * (Speed - distToTarget);
						}
					}
					// Otherwise, we're just moving along the path and not close to our target
					else
					{
						WorldPosition += velocity * Speed;
					}
				}
				// Next cell is occupied — use local avoidance for mobile units, full re-path for terrain/buildings
				else
				{
					// Walkable but not buildable = blocked by a mobile unit (temporary obstacle)
					if (GameManager.Instance.Map.IsGridPositionWalkable(nextTarget))
					{
						localAvoidWaitFrames++;

						// Phase 1: Wait a few frames — the blocker may move on its own
						if (localAvoidWaitFrames <= 3)
							return;

						// Phase 2: Try to sidestep around the blocker
						Vector3Int? sidestep = FindLocalSidestep();
						if (sidestep.HasValue)
						{
							path.Insert(0, sidestep.Value);
							localAvoidWaitFrames = 0;
							return;
						}

						// Phase 3: After extended wait, fall back to full re-path
						if (localAvoidWaitFrames > 10)
						{
							UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
							localAvoidWaitFrames = 0;
						}
					}
					else
					{
						// Not walkable = terrain or building — full re-path immediately
						localAvoidWaitFrames = 0;
						UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
					}
				}
			}
		}

		/// <summary>
		/// Find a free adjacent cell that makes progress toward the next path waypoint.
		/// Returns null if no suitable sidestep exists.
		/// </summary>
		private Vector3Int? FindLocalSidestep()
		{
			// Aim toward the cell after the blocked one, or the blocked cell itself
			Vector3Int target = path.Count > 1 ? path[1] : path[0];

			Vector3Int bestStep = Vector3Int.zero;
			float bestDist = float.MaxValue;
			bool found = false;

			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0) continue;
					Vector3Int candidate = GridPosition + new Vector3Int(dx, dy, 0);
					if (Utility.IsValidGridLocation(candidate)
						&& GameManager.Instance.Map.IsGridPositionBuildable(candidate))
					{
						float dist = Vector3.Distance((Vector3)candidate, (Vector3)target);
						if (dist < bestDist)
						{
							bestDist = dist;
							bestStep = candidate;
							found = true;
						}
					}
				}
			}

			return found ? (Vector3Int?)bestStep : null;
		}

		private void UpdateDebuggingInfo()
		{
			// Enable/disable debugging
			gameObject.GetComponentInChildren<Canvas>().enabled = HasDebugging;
			if (HasDebugging)
			{
				var textAreas = gameObject.GetComponentsInChildren<Text>().ToList();
				foreach (Text textArea in textAreas)
				{
					if (textArea.name == "Unit Number")
					{
						textArea.text = UnitNbr.ToString();
					}
					else if (textArea.name == "State Label")
					{
						textArea.text = CurrentAction.ToString()[0].ToString();
					}
					else if (textArea.name == "State Variable")
					{
						switch (CurrentAction)
						{
							case UnitAction.IDLE:
								textArea.text = "";
								break;
							case UnitAction.ATTACK:
								textArea.text = totalDamage.ToString("0.0");
								break;
							case UnitAction.BUILD:
								textArea.text = taskTime.ToString("0.0");
								break;
							case UnitAction.GATHER:
								textArea.text = totalGold.ToString("0.0");
								break;
							case UnitAction.MOVE:
								textArea.text = path.Count.ToString();
								break;
							case UnitAction.TRAIN:
								textArea.text = taskTime.ToString("0.0");
								break;
						}
					}
					else if (textArea.name == "Health Value")
					{
						textArea.text = Health.ToString("0.0");
					}
				}
			}
		}

		/// <summary>
		/// Update the path to the target with exponential backoff on failure.
		/// First retry is fast (transient blocks), subsequent retries slow down.
		/// After repeated failures, the unit goes idle.
		/// Use forceImmediate=true to bypass the cooldown throttle (for initial path computation).
		/// </summary>
		private void UpdatePath(Vector3Int gridPosition, UnitType targetUnitType, Vector3Int targetGridPos, bool forceImmediate = false)
		{
			int cooldown = (60 / Constants.GAME_SPEED) * pathBackoffMultiplier;
			if (forceImmediate || pathUpdateCounter > cooldown)
			{
				pathUpdateCounter = 0;
				path = GameManager.Instance.Map.GetPathToUnit(GridPosition, targetUnitType, targetGridPos);

				if (path.Count == 0)
				{
					pathFailCount++;
					pathBackoffMultiplier = Math.Min(pathBackoffMultiplier * 2, 8);

					if (pathFailCount >= 5)
					{
						CurrentAction = UnitAction.IDLE;
						pathFailCount = 0;
						pathBackoffMultiplier = 1;
					}
				}
				else
				{
					pathFailCount = 0;
					pathBackoffMultiplier = 1;
				}
			}
		}

		/// <summary>
		/// Update the move task
		/// </summary>
		private void UpdateMove()
		{
			if (path == null || path.Count == 0)
			{
				CurrentAction = UnitAction.IDLE;
			}
		}

		#endregion
	}
}
