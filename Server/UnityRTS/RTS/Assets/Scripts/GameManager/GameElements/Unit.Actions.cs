using AgentSDK;
using GameManager.EnumTypes;
using UnityEngine;

namespace GameManager.GameElements
{
	public partial class Unit
	{
		#region Start Actions

		/// <summary>
		/// Start training another unit
		/// </summary>
		/// <param name="args">arguments for the training task</param>
		internal void StartTraining(TrainEventArgs args)
		{
			// If this unit is currently idle, train the new entity
			if (CurrentAction == UnitAction.IDLE && CanTrain && IsBuilt)
			{
				// If we can train this type of unit
				if (Constants.TRAINS[UnitType].Contains(args.UnitType)
					&& Agent.GetComponent<AgentController>().Agent.Gold >= (int)Constants.COST[args.UnitType])
				{
					// Set the training task and start the timer
					CurrentAction = UnitAction.TRAIN;
					taskTime = 0f;
					taskUnitType = args.UnitType;
					Agent.GetComponent<AgentController>().Agent.Gold -= (int)Constants.COST[args.UnitType];
				}
			}
		}

		/// <summary>
		/// Start building another unit
		/// </summary>
		/// <param name="args">arguments for the building task</param>
		internal void StartBuilding(BuildEventArgs args)
		{
			// If this unit is currently idle, build the new unit
			if (CurrentAction != UnitAction.BUILD
				&& CanBuild
				&& CanBuildUnit(args.UnitType)
				&& GameManager.Instance.Map.IsAreaBuildable(args.UnitType, args.TargetPosition)
				&& Agent.GetComponent<AgentController>().Agent.Gold >= (int)Constants.COST[args.UnitType])
			{
				TargetGridPos = args.TargetPosition;
				TargetUnitType = args.UnitType;

				// Get the path to a neighbor of the unit to build
				UpdatePath(GridPosition, TargetUnitType, TargetGridPos);

				// If there is a path to the open cell, head toward it
				if (path.Count > 0)
				{
					currentBuilding = GameManager.Instance.Units.PlaceUnit(Agent, args.TargetPosition, args.UnitType, Color.white);
					CurrentAction = UnitAction.BUILD;
					buildPhase = BuildPhase.TO_POSITION;
					taskUnitType = args.UnitType;
					TargetGridPos = args.TargetPosition;
					Agent.GetComponent<AgentController>().Agent.Gold -= (int)Constants.COST[taskUnitType];
				}
			}
		}

		/// <summary>
		/// Start moving this agent
		/// </summary>
		/// <param name="args">arguments for moving task</param>
		internal void StartMoving(MoveEventArgs args)
		{
			if (CurrentAction != UnitAction.BUILD
				&& CanMove)
			{
				path = GameManager.Instance.Map.GetPathBetweenGridPositions(GridPosition, args.Target);
				if (path.Count > 0)
				{
					TargetGridPos = args.Target;
					TargetUnitType = args.UnitType;
					CurrentAction = UnitAction.MOVE;
				}
			}
		}

		/// <summary>
		/// Start gathering a resource
		/// </summary>
		/// <param name="args">arguments for the gathering task</param>
		internal void StartGathering(GatherEventArgs args)
		{
			if (CurrentAction != UnitAction.BUILD
				&& CanGather
				&& GameManager.Instance.Units.GetUnit(args.ResourceUnit.UnitNbr) != null
				&& GameManager.Instance.Units.GetUnit(args.BaseUnit.UnitNbr) != null)
			{
				TargetGridPos = args.ResourceUnit.GridPosition;
				TargetUnitType = args.ResourceUnit.UnitType;

				// Get a safe position to stand to build the unit
				UpdatePath(GridPosition, args.ResourceUnit.UnitType, TargetGridPos);

				// Set the mine and base for this task
				MineUnit = GameManager.Instance.Units.GetUnit(args.ResourceUnit.UnitNbr);
				BaseUnit = GameManager.Instance.Units.GetUnit(args.BaseUnit.UnitNbr);
				CurrentAction = UnitAction.GATHER;
				gatherPhase = GatherPhase.TO_MINE;
			}
			else if (!(CurrentAction == UnitAction.IDLE || CurrentAction == UnitAction.MOVE))
			{
				GameManager.Instance.Log("GATHER ERROR: Unit " + UnitNbr + " is busy and can't GATHER", this.gameObject);
			}
			else if (!CanGather)
			{
				GameManager.Instance.Log("GATHER ERROR: Unit " + UnitNbr + " is not allowed to GATHER", this.gameObject);
			}
			else
			{
				GameManager.Instance.Log("GATHER ERROR: resource or base is null", this.gameObject);
			}
		}

		/// <summary>
		/// Start attacking another agent
		/// </summary>
		/// <param name="args">arguments for attacking task</param>
		internal void StartAttacking(AttackEventArgs args)
		{
			if (CurrentAction != UnitAction.BUILD && CanAttack)
			{
				UpdatePath(GridPosition, args.Target.GetComponent<Unit>().UnitType,
					args.Target.GetComponent<Unit>().GridPosition);

				if (path.Count > 0)
				{
					TargetGridPos = args.Target.GetComponent<Unit>().GridPosition;
					TargetUnitType = args.Target.GetComponent<Unit>().UnitType;
					CurrentAction = UnitAction.ATTACK;
					AttackUnit = args.Target;
					damage = 0.0f;
					totalDamage = 0.0f;
				}
				else
				{
					GameManager.Instance.Log("ATTACK ERROR: could not find path from " + GridPosition
						+ " to " + args.Target.GetComponent<Unit>().GridPosition, this.gameObject);
				}
			}
		}

		#endregion
	}
}
