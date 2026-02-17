using System.Collections.Generic;
using AgentSDK;
using GameManager.EnumTypes;
using UnityEngine;

namespace GameManager.GameElements
{
	public partial class Unit
	{
		/// <summary>
		/// Update the attack task
		/// </summary>
		private void UpdateAttack()
		{
			// If this unit we are attacking no longer exists, go to idle
			if (AttackUnit == null
				|| GameManager.Instance.Units.GetUnit(AttackUnit.GetComponent<Unit>().UnitNbr) == null
				|| AttackUnit.GetComponent<Unit>().Health <= 0)
			{
				path.Clear();
				CurrentAction = UnitAction.IDLE;
				return;
			}

			// If we're close enough to the unit, attack it and stop moving
			if (Vector3.Distance(AttackUnit.GetComponent<Unit>().GridPosition, GridPosition)
				< Constants.ATTACK_RANGE[UnitType])
			{
				path.Clear();

				// Attack this unit
				damage += (Time.deltaTime * Constants.DAMAGE[UnitType]);
				if (damage > 1)
				{
					AttackUnit.GetComponent<Unit>().Health -= (int)damage;
					totalDamage += damage;
					damage -= (int)damage;
				}

				// If the enemy unit is dead, stop attacking
				if (AttackUnit.GetComponent<Unit>().Health <= 0)
				{
					CurrentAction = UnitAction.IDLE;
				}
			}
			// Otherwise, we're too far from the unit, recalculate a path to it
			else //if (Vector3.Distance(AttackUnit.GetComponent<Unit>().GridPosition, GridPosition)
				 //	>= Constants.ATTACK_RANGE[UnitType])
			{
				TargetGridPos = AttackUnit.GetComponent<Unit>().GridPosition;

				// Find a position near the unit to attack
				UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
			}
		}

		/// <summary>
		/// Update the build task
		/// </summary>
		private void UpdateBuild()
		{
			if (path.Count != 0)
				return;

			// If we are moving to the position
			if (buildPhase == BuildPhase.TO_POSITION)
			{
				// If we're at the end of our path, start building
				if (path.Count == 0) // && IsNeighborOfUnit(taskLocation))
				{
					path.Clear();
					taskTime = 0f;
					buildPhase = BuildPhase.BUILDING;
				}
			}
			else if (buildPhase == BuildPhase.BUILDING)
			{
				taskTime += Time.deltaTime;

				// if we're building a unit and we have finished the task
				if (taskTime >= Constants.CREATION_TIME[taskUnitType])
				{
					if (currentBuilding == null)
					{
						return;
					}

					currentBuilding.GetComponent<Unit>().IsBuilt = true;
					currentBuilding.GetComponent<Animator>().SetBool("IsBuilt", IsBuilt);
					path.Clear();
					CurrentAction = UnitAction.IDLE;
					currentBuilding = null;
				}
			}
		}

		/// <summary>
		/// Update the train task
		/// </summary>
		private void UpdateTrain()
		{
			taskTime += Time.deltaTime;

			// if we're training an agent and we have finished the task
			if (taskTime >= Constants.CREATION_TIME[taskUnitType])
			{
				var positions = GameManager.Instance.Map.GetBuildableGridPositionsNearUnit(UnitType, GridPosition);

				// Find a cell near us to spawn the trained troop
				if (positions.Count > 0)
				{
					GameManager.Instance.Units.PlaceUnit(Agent, positions[0], taskUnitType, Color);
					path.Clear();
					CurrentAction = UnitAction.IDLE;
				}
			}
		}

		/// <summary>
		/// Update the gather task
		/// </summary>
		private void UpdateGather()
		{
			if (path.Count != 0)
				return;

			// If we're headed to the mine
			if (gatherPhase == GatherPhase.TO_MINE)
			{
				// If there is no mine
				if (MineUnit == null || MineUnit.GetComponent<Unit>().Health <= 0)
				{
					path.Clear();
					CurrentAction = UnitAction.IDLE;
				}
				// If we've exhausted our path
				else if (path.Count == 0)
				{
					// If we've just arrived at the mine
					if (GameManager.Instance.Map.IsNeighborOfUnit(GridPosition, TargetUnitType, TargetGridPos))
					{
						gatherPhase = GatherPhase.MINING;
						minedGold = 0.0f;
						taskTime = 0f;
					}
					else
					{
						UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
					}
				}
			}
			// if we're currently mining
			else if (gatherPhase == GatherPhase.MINING)
			{
				// If the mine is empty and gone
				if (MineUnit == null || MineUnit.GetComponent<Unit>().Health <= 0)
				{
					if (BaseUnit == null)
					{
						path.Clear();
						CurrentAction = UnitAction.IDLE;
						return;
					}

					gatherPhase = GatherPhase.TO_BASE;
					TargetUnitType = UnitType.BASE;
					TargetGridPos = BaseUnit.GetComponent<Unit>().GridPosition;
					UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
				}
				// Otherwise if there is a mine, collect totalGold
				else if (MineUnit.GetComponent<Unit>().Health > 0)
				{
					taskTime += Time.deltaTime;
					minedGold += Time.deltaTime
								 * (MiningSpeed + (MiningSpeed * Constants.MINING_BOOST
												   * GameManager.Instance.Units.GetUnitNbrsOfType(UnitType.REFINERY, Agent.GetComponent<AgentController>().Agent.AgentNbr).Count));
					if (minedGold >= 1)
					{
						MineUnit.GetComponent<Unit>().Health -= (int)minedGold;
						totalGold += (int)minedGold;
						minedGold -= (int)minedGold;
					}

					// If we've reached our mining capacity
					if (totalGold >= MiningCapacity && BaseUnit != null)
					{
						gatherPhase = GatherPhase.TO_BASE;
						TargetUnitType = UnitType.BASE;
						TargetGridPos = BaseUnit.GetComponent<Unit>().GridPosition;
						UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
					}
					else if (totalGold >= MiningCapacity && BaseUnit == null)
					{
						path.Clear();
						CurrentAction = UnitAction.IDLE;
					}
				}
			}
			else // if (gatherPhase == GatherPhase.TO_BASE
			{
				// If we're at the base, deposit any totalGold, head back to the mine
				if (BaseUnit != null
					&& GameManager.Instance.Map.IsNeighborOfUnit(GridPosition, TargetUnitType, TargetGridPos))
				{
					Agent.GetComponent<AgentController>().Agent.Gold += totalGold;
					totalGold = 0;

					// Go back to the mine
					if (MineUnit != null)
					{
						gatherPhase = GatherPhase.TO_MINE;
						TargetUnitType = UnitType.MINE;
						TargetGridPos = MineUnit.GetComponent<Unit>().GridPosition;
						UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
					}
					else
					{
						path.Clear();
						CurrentAction = UnitAction.IDLE;
					}
				}
				else if (BaseUnit != null)
				{
					TargetUnitType = UnitType.BASE;
					TargetGridPos = BaseUnit.GetComponent<Unit>().GridPosition;
					UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
				}
				else if (BaseUnit == null)
				{
					path.Clear();
					CurrentAction = UnitAction.IDLE;
				}
			}
		}
	}
}
