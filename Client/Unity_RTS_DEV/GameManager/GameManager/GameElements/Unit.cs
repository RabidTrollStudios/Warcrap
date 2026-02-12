using System;
using System.Collections.Generic;
using System.Linq;
using GameManager.EnumTypes;
using GameManager.Graph;
using UnityEngine;
using UnityEngine.UI;

namespace GameManager.GameElements
{
	/// <summary>
	/// Represents a single unit (troop or building) in the game
	/// </summary>
	public class Unit : MonoBehaviour, IColorable
	{
		#region Static Variables

		/// <summary>
		/// Does this unit have debugging information visible
		/// </summary>
		public static bool HasDebugging { get; set; }

		#endregion

		#region Properties

		/// <summary>
		/// Unique number for this unit
		/// </summary>
		public int UnitNbr { get; internal set; }

		/// <summary>
		/// Type of this unit
		/// </summary>
		public UnitType UnitType { get; internal set; }

		/// <summary>
		/// Is building of this unit complete?
		/// </summary>
		public bool IsBuilt { get; internal set; }

		/// <summary>
		/// Color of this agent
		/// </summary>
		public Color Color { get; internal set; }

		/// <summary>
		/// Change this unit's color
		/// internal by interface
		/// </summary>
		/// <param name="color">new color</param>
		public void ChangeColor(Color color)
		{
			Color = color;
		}

		/// <summary>
		/// World position of this agent
		/// </summary>
		public Vector3 WorldPosition
		{
			get => transform.position;
			internal set => transform.position = value;
		}

		/// <summary>
		/// Position on the grid of this agent
		/// </summary>
		public Vector3Int GridPosition { get; internal set; }

		/// <summary>
		/// Current hit points of this unit
		/// </summary>
		public float Health { get; internal set; }

		/// <summary>
		/// Agent that owns this unit
		/// </summary>
		public GameObject Agent { get; internal set; }

		/// <summary>
		/// Grid position that this unit is targetting
		/// </summary>
		public Vector3Int TargetGridPos { get; internal set; }

		/// <summary>
		/// Unit type that this unit is targetting
		/// </summary>
		public UnitType TargetUnitType { get; internal set; }

		#endregion

		#region Data Members

		// State Variables
		private float taskTime;
		private Animator animator;

		// Mining Variables
		private int totalGold = 0;
		private float minedGold = 0.0f;
		private GatherPhase gatherPhase = GatherPhase.TO_MINE;

		// Training Variables
		// Building Variables
		private UnitType taskUnitType;
		private BuildPhase buildPhase;
		private GameObject currentBuilding;

		// Attacking Variables
		private int attackUnitNbr = -1;
		private float damage = 0.0f;
		private float totalDamage = 0.0f;

		// Path variables
		private int baseUnit = -1;
		private int mineUnit = -1;
		private List<Vector3Int> path;
		private Vector3 velocity;
		private int pathUpdateCounter = 0;

		#endregion

		#region Constant Properties

		/// <summary>
		/// Movement speed of the unit
		/// </summary>
		public float Speed => Constants.MOVING_SPEED[UnitType];

		/// <summary>
		/// Mining speed of the unit
		/// </summary>
		public float MiningSpeed => Constants.MINING_SPEED[UnitType];

		/// <summary>
		/// Carrying capacity of a miner
		/// </summary>
		public float MiningCapacity => Constants.MINING_CAPACITY[UnitType];

		/// <summary>
		/// Cost to train or build the unit
		/// </summary>
		public float Cost => Constants.COST[UnitType];

		/// <summary>
		/// Time to train or build the unit
		/// </summary>
		public float CreationTime => Constants.CREATION_TIME[UnitType];

		/// <summary>
		/// Unit dependencies that must be satisfied before
		/// building or training this unit
		/// </summary>
		public List<UnitType> Dependencies => Constants.DEPENDENCY[UnitType];

		/// <summary>
		/// Can this unit move
		/// </summary>
		public bool CanMove => Constants.CAN_MOVE[UnitType];

		/// <summary>
		/// Can this unit build others
		/// </summary>
		public bool CanBuild => Constants.CAN_BUILD[UnitType];

		/// <summary>
		/// Can this unit train others
		/// </summary>
		public bool CanTrain => Constants.CAN_TRAIN[UnitType];

		/// <summary>
		/// Can this unit attack others
		/// </summary>
		public bool CanAttack => Constants.CAN_ATTACK[UnitType];

		/// <summary>
		/// Can this unit gather
		/// </summary>
		public bool CanGather => Constants.CAN_GATHER[UnitType];

		/// <summary>
		/// Which Units does this unit Train
		/// </summary>
		public List<UnitType> Trains => Constants.TRAINS[UnitType];

		/// <summary>
		/// Which Units does this unit Train
		/// </summary>
		public List<UnitType> Builds => Constants.BUILDS[UnitType];

		#endregion

		#region Properties

		/// <summary>
		/// Velocity of this unit
		/// </summary>
		public Vector3 Velocity { get; internal set; }

		/// <summary>
		/// Current action of this unit
		/// </summary>
		public UnitAction CurrentAction { get; internal set; }

		/// <summary>
		/// Current main base of this unit
		/// </summary>
		public Unit BaseUnit
		{
			get
			{
				Unit unit = GameManager.Instance.GetUnit(baseUnit);
				if (baseUnit != -1 && unit != null)
					return unit;
				else
					return null;
			}
			internal set
			{
				if (value == null)
					baseUnit = -1;
				else
					baseUnit = value.GetComponent<Unit>().UnitNbr;
			}
		}

		/// <summary>
		/// Current main mine of this unit
		/// null otherwise
		/// </summary>
		public Unit MineUnit
		{
			get
			{
				Unit unit = GameManager.Instance.GetUnit(mineUnit);
				if (mineUnit != -1 && unit != null)
					return unit;
				else
					return null;
			}
			internal set
			{
				if (value == null)
					mineUnit = -1;
				else
					mineUnit = value.GetComponent<Unit>().UnitNbr;
			}
		}

		/// <summary>
		/// Unit that this unit is attacking
		/// null otherwise
		/// </summary>
		public Unit AttackUnit
		{
			get
			{
				Unit unit = GameManager.Instance.GetUnit(attackUnitNbr);
				if (attackUnitNbr != -1 && unit != null)
					return unit;
				else
					return null;
			}
			internal set
			{
				if (value == null)
					attackUnitNbr = -1;
				else
					attackUnitNbr = value.GetComponent<Unit>().UnitNbr;
			}
		}

		/// <summary>
		/// CanTrainUnit asks if the current unit
		/// can train the type of unit provided by the parameter
		/// </summary>
		/// <param name="UnitType">type of unit to train</param>
		/// <returns>true if trainable and false otherwise</returns>
		public bool CanTrainUnit(UnitType UnitType)
		{
			return Trains.Contains(UnitType);
		}

		/// <summary>
		/// CanBuildUnit asks if the current unit
		/// can build the type of unit provided by the parameter
		/// </summary>
		/// <param name="UnitType">type of unit to train</param>
		/// <returns>true if buildable, false otherwise</returns>
		public bool CanBuildUnit(UnitType UnitType)
		{
			return Builds.Contains(UnitType);
		}

		#endregion

		#region Initializers

		/// <summary>
		/// InitializeRound this unit
		/// </summary>
		/// <param name="agent">agent that owns this unit</param>
		/// <param name="gridPosition">initial position of this unit</param>
		/// <param name="unitType">type of this unit</param>
		/// <param name="unitNbr">the unique number for this unit</param>
		internal void Initialize(GameObject agent, Vector3Int gridPosition, UnitType unitType, int unitNbr)
		{
			HasDebugging = GameManager.Instance.HasUnitDebugging;
			Agent = agent;
			UnitNbr = unitNbr;
			this.velocity = Vector3.zero;
			this.CurrentAction = UnitAction.IDLE;
			path = new List<Vector3Int>();
			UnitType = unitType;
			if (Constants.BUILDS[UnitType.WORKER].Contains(UnitType))
			{
				IsBuilt = false;
			}
			else
			{
				IsBuilt = true;
			}

			GridPosition = gridPosition;
			Health = Constants.HEALTH[UnitType];
			animator = gameObject.GetComponent<Animator>();
		}

		#endregion

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
				&& GameManager.Instance.IsAreaBuildable(args.UnitType, args.TargetPosition)
				&& Agent.GetComponent<AgentController>().Agent.Gold >= (int)Constants.COST[args.UnitType])
			{
				TargetGridPos = args.TargetPosition;
				TargetUnitType = args.UnitType;

				// Get the path to a neighbor of the unit to build
				UpdatePath(GridPosition, TargetUnitType, TargetGridPos);

				// If there is a path to the open cell, head toward it
				if (path.Count > 0)
				{
					currentBuilding = GameManager.Instance.PlaceUnit(Agent, args.TargetPosition, args.UnitType, Color.white);
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
				path = GameManager.Instance.GetPathBetweenGridPositions(GridPosition, args.Target);
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
				&& GameManager.Instance.GetUnit(args.ResourceUnit.UnitNbr) != null
				&& GameManager.Instance.GetUnit(args.BaseUnit.UnitNbr) != null)
			{
				TargetGridPos = args.ResourceUnit.GridPosition;
				TargetUnitType = args.ResourceUnit.UnitType;

				// Get a safe position to stand to build the unit
				UpdatePath(GridPosition, args.ResourceUnit.UnitType, TargetGridPos);

				// Set the mine and base for this task
				MineUnit = GameManager.Instance.GetUnit(args.ResourceUnit.UnitNbr);
				BaseUnit = GameManager.Instance.GetUnit(args.BaseUnit.UnitNbr);
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


		#region Update Methods

		/// <summary>
		/// Update this unit
		/// </summary>
		internal void Update()
		{
			MineUnit = GameManager.Instance.GetUnit(mineUnit);
			BaseUnit = GameManager.Instance.GetUnit(baseUnit);

			pathUpdateCounter++;
			HasDebugging = GameManager.Instance.HasUnitDebugging;

			UpdateDebuggingInfo();

			// If this unit is dead, destroy it
			if (Health <= 0)
			{
				GameManager.Instance.DestroyUnit(gameObject);
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
				// If the next cell in the path is buildable, move forward
				Vector3Int nextTarget = path[0];
				if (GameManager.Instance.IsGridPositionBuildable(nextTarget)) // || oldGridPosition == nextTarget)
				{
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
						GameManager.Instance.SetAreaBuildability(gameObject.GetComponent<Unit>().UnitType, nextTarget, false);
						GameManager.Instance.SetAreaBuildability(gameObject.GetComponent<Unit>().UnitType, GridPosition, true);
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
				// If the next cell is NOT buildable, try to find a new path
				else if (!GameManager.Instance.IsGridPositionBuildable(nextTarget))
				{
					UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
				}
			}
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
		/// Update the path to the target but wait a few frames between each call
		/// </summary>
		private void UpdatePath(Vector3Int gridPosition, UnitType targetUnitType, Vector3Int targetGridPos)
		{
			if (pathUpdateCounter > 60 / Constants.GAME_SPEED)
			{
				pathUpdateCounter = 0;
				path = GameManager.Instance.GetPathToUnit(GridPosition, targetUnitType, targetGridPos);
			}
		}

		/// <summary>
		/// Update the move task
		/// </summary>
		private void UpdateMove()
		{
			GameManager.Instance.Log("UpdateMove: " + path.Count, this.gameObject);
			if (path == null || path.Count == 0)
			{
				CurrentAction = UnitAction.IDLE;
			}
		}

		/// <summary>
		/// Update the attack task
		/// </summary>
		private void UpdateAttack()
		{
			// If this unit we are attacking no longer exists, go to idle
			if (AttackUnit == null
				|| GameManager.Instance.GetUnit(AttackUnit.GetComponent<Unit>().UnitNbr) == null
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
				var positions = GameManager.Instance.GetBuildableGridPositionsNearUnit(UnitType, GridPosition);

				// Find a cell near us to spawn the trained troop
				if (positions.Count > 0)
				{
					GameManager.Instance.PlaceUnit(Agent, positions[0], taskUnitType, Color);
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
					if (GameManager.Instance.IsNeighborOfUnit(GridPosition, TargetUnitType, TargetGridPos))
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
												   * GameManager.Instance.GetUnitNbrsOfType(UnitType.REFINERY, Agent.GetComponent<AgentController>().Agent.AgentNbr).Count));
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
					&& GameManager.Instance.IsNeighborOfUnit(GridPosition, TargetUnitType, TargetGridPos))
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

		#endregion
	}
}