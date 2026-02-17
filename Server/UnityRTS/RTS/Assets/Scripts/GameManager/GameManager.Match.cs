using AgentSDK;
using GameManager.GameElements;
using Preloader;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace GameManager
{
	public partial class GameManager
	{
		#region Match Initialization

		IEnumerator DropIntroVersus()
		{
			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = true;

			yield return new WaitForSeconds(1.5f);

			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = false;
		}

		/// <summary>
		/// Called once at the beginning of each match (sequence of rounds)
		/// </summary>
		private void InitializeMatch()
		{
			if (RandomizeAgentsAsOrc)
			{
				OrcDllName = "";
				dllNames = agentLoader.GetDLLNamesFromDir(this.gameObject);

				if (dllNames.Count > 0)
				{
					OrcDllName = dllNames[Random.Range(0, dllNames.Count)];
					isHumanUsingDllNames = false;
				}
				else
				{
					Log("ERROR: No DLLs to play against, exiting.", this.gameObject);
					Application.Quit();
				}
			}

			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = false;
			HasUnitDebugging = false;
			HasAgentDebugging = true;

			NbrOfAgents = 0;

			mapManager.GenerateGraph(Prefabs.Grid, this.gameObject);
			mapManager.InfluenceMap.gameObject.SetActive(false);

			AgentWins = new Dictionary<string, int>();
			AgentWins[Constants.HUMAN_ABBR] = 0;
			AgentWins[Constants.ORC_ABBR] = 0;

			unitManager.UnitPrefabs = new Dictionary<int, Dictionary<UnitType, GameObject>>();
			Agents = new Dictionary<int, GameObject>();

			// Randomly select one player to be instantiated first, for fairness
			if (Random.Range(0, 2) == 0)
			{
				CreateAgent(Constants.HUMAN_ABBR, HumanDllName, Prefabs.HumanPlayerPrefab, unitManager.HumanUnitPrefabs, HumanDebuggerCanvas);
				CreateAgent(Constants.ORC_ABBR, OrcDllName, Prefabs.OrcPlayerPrefab, unitManager.OrcUnitPrefabs, OrcDebuggerCanvas);
			}
			else
			{
				CreateAgent(Constants.ORC_ABBR, OrcDllName, Prefabs.OrcPlayerPrefab, unitManager.OrcUnitPrefabs, OrcDebuggerCanvas);
				CreateAgent(Constants.HUMAN_ABBR, HumanDllName, Prefabs.HumanPlayerPrefab, unitManager.HumanUnitPrefabs, HumanDebuggerCanvas);
			}

			Prefabs.HumanLabelText.text = Constants.HUMAN_ABBR + " " + HumanDllName;
			Prefabs.OrcLabelText.text = Constants.ORC_ABBR + " " + OrcDllName;

			Prefabs.GameOverUI.GetComponentInChildren<Text>().text
					= Prefabs.HumanLabelText.text + "\nvs\n" + Prefabs.OrcLabelText.text;

			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().InitializeMatch();
				agent.GetComponent<AgentController>().Agent.OpenCommandLog();
			}

			NbrOfRounds = 0;

			InitializeRound();

			StartCoroutine(DropIntroVersus());
		}

		/// <summary>
		/// Instantiate an agent
		/// </summary>
		private void CreateAgent(string agentName, string agentDLLName,
			GameObject playerPrefab, Dictionary<UnitType, GameObject> playerPrefabs, Canvas debuggerCanvas)
		{
			GameObject agentObject = Instantiate(playerPrefab);
			agentObject.GetComponent<AgentController>().InitializeAgent(
				agentLoader.LoadDLL(agentName, agentDLLName, this.gameObject),
				agentName, agentDLLName, NbrOfAgents++, debuggerCanvas, agentLoader.PathToDLLs);
			Agents.Add(agentObject.GetComponent<AgentController>().Agent.AgentNbr, agentObject);
			unitManager.UnitPrefabs.Add(agentObject.GetComponent<AgentController>().Agent.AgentNbr, playerPrefabs);
		}

		private void RecreateAgent(string agentName, string agentDLLName, int agentNbr,
			GameObject playerPrefab, Dictionary<UnitType, GameObject> playerPrefabs, Canvas debuggerCanvas)
		{
			Agents[agentNbr].GetComponent<AgentController>().Agent.CloseCommandLog();
			Destroy(Agents[agentNbr].GetComponent<AgentController>().Agent.gameObject);
			Destroy(Agents[agentNbr].GetComponent<AgentController>().gameObject);

			GameObject agentObject = Instantiate(playerPrefab);
			agentObject.GetComponent<AgentController>().InitializeAgent(
				agentLoader.LoadDLL(agentName, agentDLLName, this.gameObject),
				agentName, agentDLLName, agentNbr, debuggerCanvas, agentLoader.PathToDLLs);
			Agents[agentNbr] = agentObject;
			unitManager.UnitPrefabs[agentNbr] = playerPrefabs;
			agentObject.GetComponent<AgentController>().Agent.OpenCommandLog();
		}

		#endregion

		#region Round Initialization

		/// <summary>
		/// Called once at the start of each round
		/// </summary>
		private void InitializeRound()
		{
			Log("********************************** InitializeRound **********************************", gameObject);

			gameState = GameState.PLAYING;
			TotalGameTime = 0;
			TimeToDisplayBanner = 0f;
			unitManager.ResetForRound();

			PickNextRandomAgent();

			NbrOfRounds++;

			foreach (GameObject agent in Agents.Values)
            {
	            agent.GetComponent<AgentController>().Agent.gameObject.SetActive(true);
				agent.GetComponent<AgentController>().Agent.OpenLogFile();
				agent.GetComponent<AgentController>().Agent.CmdLog?.StartRound(NbrOfRounds);
				agent.GetComponent<AgentController>().InitializeRound();
			}

			PlaceUnits();
		}

        private void PickNextRandomAgent()
        {
	        if (NbrOfRounds > 0 && dllNames != null)
	        {
		        if (isHumanUsingDllNames)
		        {
			        int agentNbr = (Agents[0].GetComponent<AgentController>().Agent.AgentDLLName == HumanDllName) ? 0 : 1;
			        HumanDllName = dllNames[Random.Range(0, dllNames.Count)];
			        RecreateAgent(Constants.HUMAN_ABBR, HumanDllName, agentNbr, Prefabs.HumanPlayerPrefab, unitManager.HumanUnitPrefabs,
				        HumanDebuggerCanvas);
			        Agents[agentNbr].GetComponent<AgentController>().InitializeMatch();
		        }
		        else
		        {
			        int agentNbr = (Agents[0].GetComponent<AgentController>().Agent.AgentDLLName == OrcDllName) ? 0 : 1;
			        OrcDllName = dllNames[Random.Range(0, dllNames.Count)];
			        RecreateAgent(Constants.ORC_ABBR, OrcDllName, agentNbr, Prefabs.OrcPlayerPrefab, unitManager.OrcUnitPrefabs,
				        OrcDebuggerCanvas);
			        Agents[agentNbr].GetComponent<AgentController>().InitializeMatch();
		        }

		        Prefabs.HumanLabelText.text = Constants.HUMAN_ABBR + " " + HumanDllName;
		        Prefabs.OrcLabelText.text = Constants.ORC_ABBR + " " + OrcDllName;

		        Prefabs.GameOverUI.GetComponentInChildren<Text>().text
			        = Constants.HUMAN_ABBR + " " + HumanDllName + "\nvs\n" + Constants.ORC_ABBR + " " + OrcDllName;
		        StartCoroutine(DropIntroVersus());
	        }
        }

        private Vector3Int FindMirroredLocation(Vector3Int position, UnitType unitType)
        {
			return new Vector3Int(mapManager.MapSize.x - Constants.UNIT_SIZE[unitType].x - position.x,
								  mapManager.MapSize.y - 2 + Constants.UNIT_SIZE[unitType].y - position.y, 0);
        }

		private void PlaceUnits()
        {
	        Vector3Int workerLocation = mapManager.GetRandomBuildableLocation(UnitType.BASE);
	        Vector3Int mineLocation = mapManager.GetRandomBuildableLocation(UnitType.MINE);
	        int initAgentNbr = Random.Range(0, 2);

	        unitManager.PlaceUnit(Agents[initAgentNbr], workerLocation, UnitType.WORKER, Color.white);
	        unitManager.PlaceUnit(Agents[initAgentNbr], mineLocation, UnitType.MINE, Color.white);
	        unitManager.PlaceUnit(Agents[(initAgentNbr + 1) % 2], FindMirroredLocation(workerLocation, UnitType.BASE), UnitType.WORKER, Color.white);
	        unitManager.PlaceUnit(Agents[(initAgentNbr + 1) % 2], FindMirroredLocation(mineLocation, UnitType.BASE), UnitType.MINE, Color.white);
        }

        /// <summary>
        /// Called after each round so agents can learn from the outcome
        /// </summary>
        private void Learn()
        {
			string winnerName = roundWinner != null
				? roundWinner.GetComponent<AgentController>().Agent.AgentName + " " + roundWinner.GetComponent<AgentController>().Agent.AgentDLLName
				: "unknown";

			if (EnableLearning)
			{
				foreach (GameObject agent in Agents.Values)
				{
		            agent.GetComponent<AgentController>().Learn();
					agent.GetComponent<AgentController>().Agent.EndLogLine();
				}
			}

			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().Agent.CmdLog?.EndRound(winnerName + " wins");
			}
		}

        #endregion

	}
}
