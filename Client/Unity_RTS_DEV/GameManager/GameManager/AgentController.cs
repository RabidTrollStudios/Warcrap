using System.Runtime.CompilerServices;
using GameManager.EnumTypes;
using UnityEngine;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("GameManager")]

namespace GameManager
{
	class AgentController : MonoBehaviour
	{
		private Canvas debuggerCanvas;

		internal Agent Agent;

		/// <summary>
		/// Link the Agent to the UI controller by giving it the Agent
		/// </summary>
		/// <param name="agent"></param>
		/// <param name="agentName"></param>
		/// <param name="agentNbr"></param>
		/// <param name="agentDLLName"></param>
		/// <param name="debuggerCanvas"></param>
		/// <param name="dllPath"></param>
		internal void InitializeAgent(GameObject agent, string agentName, string agentDLLName, int agentNbr, Canvas debuggerCanvas, string dllPath)
		{
			Agent = agent.GetComponent<Agent>();
            Agent.InitializeAgent(agentName, agentDLLName, agentNbr, dllPath);
            this.debuggerCanvas = debuggerCanvas;
        }

        /// <summary>
        /// InitializeMatch
        /// Called once at the beginning of each match.
        /// Multiple rounds make up a match between a single pair
        /// of agents.  Sets up any variables for the entire match.
        /// </summary>
        internal void InitializeMatch()
        {
            Agent.InitializeMatch();
        }

        /// <summary>
        /// InitializeRound
        /// Called once at the beginning of each round
        /// </summary>
        internal void InitializeRound()
        {
            Agent.Gold = GameManager.Instance.StartingPlayerGold;
            Agent.InitializeRound();
        }

        /// <summary>
        /// Learn
        /// Called once after each round before remaining units are destroyed
        /// </summary>
        internal void Learn()
        {
            Agent.Learn();
        }

        /// <summary>
        /// Updated
        /// Called once per frame
        /// </summary>
        public void Update()
		{
			if (Agent == null)
				return;

			// If we've enabled debugging for the planning Agent, build the data
			// You can modify these to display any data you want!  You can even
			// modify the prefab to add more rows or columns!
			if (GameManager.Instance.HasAgentDebugging)
			{
				debuggerCanvas.enabled = true;

				// Update the UI
				var debuggerTextAreas = debuggerCanvas.GetComponentsInChildren<Text>();
				foreach (var textArea in debuggerTextAreas)
				{
					switch (textArea.name)
					{
						case "Agent Name":
							textArea.text = Agent.AgentName + " " + Agent.AgentDLLName;
							break;
						case "Agent Nbr":
							textArea.text = Agent.AgentNbr.ToString();
							break;
						case "Gold Value":
							textArea.text = Agent.Gold.ToString();
							break;
						case "Workers Count":
							textArea.text = GameManager.Instance.GetUnitNbrsOfType(UnitType.WORKER, Agent.AgentNbr).Count.ToString();
							break;
						case "Soldiers Count":
							textArea.text = GameManager.Instance.GetUnitNbrsOfType(UnitType.SOLDIER, Agent.AgentNbr).Count.ToString();
							break;
						case "Archers Count":
							textArea.text = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, Agent.AgentNbr).Count.ToString();
							break;
						case "Bases Count":
							textArea.text = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, Agent.AgentNbr).Count.ToString();
							break;
						case "Barracks Count":
							textArea.text = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, Agent.AgentNbr).Count.ToString();
							break;
						case "Refinery Count":
							textArea.text = GameManager.Instance.GetUnitNbrsOfType(UnitType.REFINERY, Agent.AgentNbr).Count.ToString();
							break;
					}
				}
			}
			else
			{
				debuggerCanvas.enabled = false;
			}
		}
	}
}
