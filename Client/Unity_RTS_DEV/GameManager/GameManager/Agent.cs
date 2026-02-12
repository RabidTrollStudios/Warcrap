using GameManager.EnumTypes;
using GameManager.GameElements;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.Text;
using System.Text.RegularExpressions;


[assembly: InternalsVisibleTo("GameManager")]
[assembly: InternalsVisibleTo("AgentController")]

namespace GameManager
{

	/// <summary>
	/// Represents a Player in the game
	/// </summary>
	[Serializable]
	public abstract class Agent : MonoBehaviour
	{
		#region Public Properties

		/// <summary>
	    /// Unique number that identifies this agent
	    /// </summary>
	    public int AgentNbr { get; private set; }

        /// <summary>
        /// Name for this agent (used in debugging)
        /// </summary>
        public string AgentName { get; private set; }

		/// <summary>
		/// DLL name for this agent (used in declaring the winner)
		/// </summary>
		public string AgentDLLName { get; private set; }

		/// <summary>
		/// Number of wins this agent currently has
		/// </summary>
		public int AgentNbrWins { get; internal set; }

		private string DllPath { get; set; }

		private FileStream LogFileStream { get; set; }

		private string logFileName { get; set; }

		#endregion

		#region Public File Logging

		/// <summary>
		/// Log the learned data to a csv file
		/// </summary>
		/// <param name="str"></param>
		public void Log(string str)
		{
			if (str.Contains(","))
				str = "\"" + str + "\"";
			byte[] info = new UTF8Encoding(true).GetBytes(str + ",");
			LogFileStream.Write(info, 0, info.Length);
		}

		internal void EndLogLine()
		{
			byte[] info = new UTF8Encoding(true).GetBytes("\n");
			LogFileStream.Write(info, 0, info.Length);
		}

		internal void CloseLogFile()
		{
			LogFileStream.Close();
		}

		internal void OpenLogFile()
		{
			LogFileStream = File.Open(logFileName,FileMode.Append);
		}

		#endregion

		#region Constructors and Initialization

		/// <summary>
		/// InitializeAgent the agent's identity, this is called once at the
		/// beginning of the entire game
		/// </summary>
		/// <param name="agentName">agent's human/orc name</param>
		/// <param name="agentNbr">agent's unique number</param>
		/// <param name="dllName">agent's dll name</param>
		/// <param name="dllPath"></param>
		internal void InitializeAgent(string agentName, string dllName, int agentNbr, string dllPath)
        {
            AgentName = agentName;
            AgentNbr = agentNbr;
			AgentDLLName = dllName;
			DllPath = dllPath;
			AgentNbrWins = 0;
			logFileName = dllPath + Path.AltDirectorySeparatorChar + "PlanningAgent_" + dllName + ".csv";

			// Create a new file by appending a number if it already exists
			if (File.Exists(logFileName))
			{
				// Only get files that begin with the letter PlanningAgent_dllName
				string[] files = Directory.GetFiles(dllPath + Path.AltDirectorySeparatorChar, "PlanningAgent_" + dllName + "*.csv");
				int max = 0;

				Regex rx = new Regex(@"PlanningAgent_" + dllName + @"_(\d)\.csv",
					RegexOptions.Compiled | RegexOptions.IgnoreCase);

				foreach (string file in files)
				{
					MatchCollection mc = rx.Matches(file);

					//GameManager.Instance.Log("mc.count: " + mc.Count.ToString(), this.gameObject);
					foreach (Match m in mc)
					{
						//GameManager.Instance.Log("m.groups[1].Value: " + m.Groups[1].Value, this.gameObject);
						int value;
						if (Int32.TryParse(m.Groups[1].Value, out value) && max < value)
						{
							max = value;
						}
					}
				}
				logFileName = dllPath + Path.AltDirectorySeparatorChar + "PlanningAgent_" + dllName + "_" + (++max) + ".csv";
			}
			GameManager.Instance.Log("Creating: " + logFileName, this.gameObject);
			//LogFileStream = File.Create(logFileName);
        }

        /// <summary>
        /// InitializeMatch
        /// This method must be overriden by
        /// the PlanningAgent and is called at the beginning of each matching
        /// of two agents.  Each match is comprised of multiple rounds.  This
        /// is called only once to initialize the agent regardless of the
        /// number of rounds.
        /// </summary>
        public abstract void InitializeMatch();

        /// <summary>
        /// InitializeRound
        /// This method must be overridden by the PlanningAgent and is
        /// called at the beginning of each round in a game.  Multiple
        /// rounds make a single match between two agents.
        /// </summary>
        public abstract void InitializeRound();

        /// <summary>
        /// Learn
        /// This method is called at the end of each match BEFORE any
        /// remaining troops are destroyed, so the PlanningAgent can
        /// observe the "win" state and learn from it.
        /// </summary>
        public abstract void Learn();

        #endregion

        #region Properties

        /// <summary>
        /// The amount of gold the agent currently has
        /// </summary>
        public int Gold { get; internal set; }

		/// <summary>
		/// Screen color of the agent
		/// </summary>
		internal Color Color { get; set; }

		#endregion

		#region Public Methods

		/// <summary>
		/// Updates the agent each frame
		/// </summary>
		public virtual void Update() { }

		#endregion


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
			if (!GameManager.Instance.IsGridPositionBuildable(target))
			{
				GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName
					+ " ERROR: Can't Move Unit(" + unit.UnitNbr + ") - target location " + target + " is not walkable", this.gameObject);
				return;
			}

			// Valid unit and location, Move the unit
			GameManager.Instance.Log(unit.Agent.GetComponent<AgentController>().Agent.AgentName 
						+ " Move: Unit(" + unit.UnitNbr + ") to " + target, this.gameObject);
			GameManager.Instance.MoveEventHandler(this, new MoveEventArgs(unit, unit.UnitType, target));
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
			if (!GameManager.Instance.IsAreaBuildable(unitType, target))
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
				if (GameManager.Instance.GetUnitNbrsOfType(uT).Where(
							u => GameManager.Instance.GetUnit(u).IsBuilt).ToList().Count == 0)
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
			GameManager.Instance.BuildEventHandler(this, new BuildEventArgs(unit, target, unitType));
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
			GameManager.Instance.GatherEventHandler(this, new GatherEventArgs(unit, resource, baseUnit));
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
			GameManager.Instance.TrainEventHandler(this, new TrainEventArgs(unit, unitType));
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
			GameManager.Instance.AttackEventHandler(this, new AttackEventArgs(unit, target));
		}

		#endregion
	}
}
