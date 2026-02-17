using AgentSDK;
using GameManager.GameElements;
using System.Linq;
using UnityEngine;

namespace GameManager
{
	public abstract partial class Agent
	{
		#region Event Throwers
		/// <summary>
		/// Command to move a unit to an arbitrary point on the grid
		/// </summary>
		/// <param name="unit">the unit to move</param>
		/// <param name="target">the point to move to</param>
		public void Move(Unit unit, Vector3Int target)
		{
			GameManager.Instance.Log("Trying to move agent " + target, this.gameObject);
			if (unit == null)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Move Unit(" + unit.UnitNbr + ") - is null", this.gameObject);
				return;
			}
			if (!unit.CanMove)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Move Unit(" + unit.UnitNbr + ") - can't move", this.gameObject);
				return;
			}
			if (!Utility.IsValidGridLocation(target))
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Move Unit(" + unit.UnitNbr + ") - target location " + target + " is not on map", this.gameObject);
				return;
			}
			if (!GameManager.Instance.Map.IsGridPositionBuildable(target))
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Move Unit(" + unit.UnitNbr + ") - target location " + target + " is not walkable", this.gameObject);
				return;
			}

			// Valid unit and location, Move the unit
			GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
						+ " Move: Unit(" + unit.UnitNbr + ") to " + target, this.gameObject);
			GameManager.Instance.Events.MoveEventHandler(this, new MoveEventArgs(unit, unit.UnitType, target));
		}

		/// <summary>
		/// Command to send a unit to build another unit at a particular point
		/// on the grid
		/// </summary>
		/// <param name="unit">the building unit</param>
		/// <param name="target">the location to build the new unit</param>
		/// <param name="unitType">the new type of unit to build</param>
		public void Build(Unit unit, Vector3Int target, UnitType unitType)
		{
			if (unit == null)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Build " + unitType + " with Unit(" + unit.UnitNbr + ") - is null", this.gameObject);
				return;
			}
			if (!unit.CanBuild)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Build " + unitType + " with Unit(" + unit.UnitNbr + ") - can't build", this.gameObject);
				return;
			}
			if (!unit.CanBuildUnit(unitType))
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Build " + unitType + " with Unit(" + unit.UnitNbr + ") - can't build " + unitType, this.gameObject);
				return;
			}
			if (!Utility.IsValidGridLocation(target))
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Build " + unitType + " with Unit(" + unit.UnitNbr + ") - target location " + target + " is not on map", this.gameObject);
				return;
			}
			if (!GameManager.Instance.Map.IsAreaBuildable(unitType, target))
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Build " + unitType + " with Unit(" + unit.UnitNbr + ") - target location " + target + " is not buildable", this.gameObject);
				return;
			}

			string dependencies = "";
			// Check if all the dependencies are satisfied
			foreach (UnitType uT in Constants.DEPENDENCY[unitType])
			{
				// If this unit type doesn't exist in this agent's current units
				if (GameManager.Instance.Units.GetUnitNbrsOfType(uT).Where(
							u => GameManager.Instance.Units.GetUnit(u).IsBuilt).ToList().Count == 0)
				{
					dependencies += uT + " ";
				}
			}

			if (dependencies.Length != 0)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
				                         + " ERROR: Can't Build " + unitType + " with Unit(" + unit.UnitNbr + ") - missing dependency " +
				                         dependencies, this.gameObject);
				return;
			}

			// Valid unit and location, Build the unit
			GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
						+ " Build: " + unit.UnitType + "(" + unit.UnitNbr + ") " + unitType + target, this.gameObject);
			GameManager.Instance.Events.BuildEventHandler(this, new BuildEventArgs(unit, target, unitType));
		}

		/// <summary>
		/// Command to send a unit to gather resources from a particular resource
		/// </summary>
		/// <param name="unit">the gathering unit</param>
		/// <param name="resource">the resource to gather</param>
		/// <param name="baseUnit">the base to return the resource to</param>
		public void Gather(Unit unit, Unit resource, Unit baseUnit)
		{
			if (unit == null)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Gather with Unit(" + unit.UnitNbr + ") - is null", this.gameObject);
				return;
			}
			if (resource == null)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Gather with Unit(" + unit.UnitNbr + ") - Resource(" + resource + ") is null", this.gameObject);
				return;
			}
			if (resource.UnitType != UnitType.MINE)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Gather with Unit(" + unit.UnitNbr + ") - Resource(" + resource + ") is not a mine", this.gameObject);
				return;
			}
			if (baseUnit == null)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Gather with Unit(" + unit.UnitNbr + ") - Base(" + baseUnit + ") is null", this.gameObject);
				return;
			}
			if (baseUnit.UnitType != UnitType.BASE)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Gather with Unit(" + unit.UnitNbr + ") - Base(" + baseUnit + ") is not a base", this.gameObject);
				return;
			}
			if (!unit.CanGather)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Gather with Unit(" + unit.UnitNbr + ") - " + unit.UnitType + " can't gather", this.gameObject);
				return;
			}

			GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName + " Gather: Unit("
				        + unit.UnitNbr + ") at " + resource.UnitType + resource.GridPosition + " to "
				        + baseUnit.UnitType + baseUnit.GridPosition, this.gameObject);
			GameManager.Instance.Events.GatherEventHandler(this, new GatherEventArgs(unit, resource, baseUnit));
		}

		/// <summary>
		/// Command to train a unit
		/// </summary>
		/// <param name="unit">unit that will do the training</param>
		/// <param name="unitType">type of unit to train</param>
		public void Train(Unit unit, UnitType unitType)
		{
			if (unit == null)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Train with Unit(" + unit.UnitNbr + ") - is null", this.gameObject);
				return;
			}
			if (!unit.CanTrain)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Train with Unit(" + unit.UnitNbr + ") - can't train", this.gameObject);
				return;
			}
			if (!unit.IsBuilt)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Train with Unit(" + unit.UnitNbr + ") - is not yet finished being built", this.gameObject);
				return;
			}
			if (!unit.CanTrainUnit(unitType))
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Train with Unit(" + unit.UnitNbr + ") - can't train unitType " + unitType, this.gameObject);
				return;
			}

			GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName + " Train: Unit(" + unit.UnitNbr + ") " + unitType, this.gameObject);
			GameManager.Instance.Events.TrainEventHandler(this, new TrainEventArgs(unit, unitType));
		}

		/// <summary>
		/// Command to attack another unit
		/// </summary>
		/// <param name="unit">unit that will do the attacking</param>
		/// <param name="target">unit to attack</param>
		public void Attack(Unit unit, Unit target)
		{
			if (unit == null)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Attack with Unit(" + unit.UnitNbr + ") - is null", this.gameObject);
				return;
			}
			if (target == null)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Attack with Unit(" + unit.UnitNbr + ") - target " + target + " is null", this.gameObject);
				return;
			}
			if (!unit.CanAttack)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Attack with Unit(" + unit.UnitNbr + ") - can't attack", this.gameObject);
				return;
			}
			if (target.UnitType == UnitType.MINE)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Attack with Unit(" + unit.UnitNbr + ") - can't attack a mine", this.gameObject);
				return;
			}
			if (unit.Agent.GetComponent<AgentController>().Agent.AgentNbr
				== target.Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Attack with Unit(" + unit.UnitNbr + ") - can't attack your own units", this.gameObject);
				return;
			}

			GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName + " Attack: Unit(" + unit.UnitNbr
				        + ") " + unit.GridPosition + " attacking Agent("
				        + target.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr
				        + ") Unit(" + target.UnitNbr + ") " + target.GetComponent<Unit>().GridPosition, this.gameObject);
			GameManager.Instance.Events.AttackEventHandler(this, new AttackEventArgs(unit, target));
		}

		#endregion
	}
}
