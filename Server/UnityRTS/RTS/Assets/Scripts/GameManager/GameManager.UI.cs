using AgentSDK;
using UnityEngine;
using UnityEngine.UI;

namespace GameManager
{
	public partial class GameManager
	{
		#region UI and Input

		private void UpdateTimerUI()
		{
			TotalGameTime += Time.deltaTime * Constants.GAME_SPEED;
			Prefabs.TimerText.text = TotalGameTime.ToString("0.00000");
			Prefabs.SpeedText.text = Constants.GAME_SPEED.ToString();
		}

		private void ProcessUserInput()
		{
			if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
			{
				Log("Setting Agent Debugging: " + HasAgentDebugging, this.gameObject);
				HasAgentDebugging = !HasAgentDebugging;
			}

			if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
			{
				Log("Setting Unit Debugging: " + HasUnitDebugging, this.gameObject);
				HasUnitDebugging = !HasUnitDebugging;
			}

			if ((Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus)
												  || Input.GetKeyDown(KeyCode.KeypadPlus))
				&& Constants.GAME_SPEED < Constants.MAX_GAME_SPEED)
			{
				Constants.GAME_SPEED += 1;
				Log("Increasing GameSpeed: " + Constants.GAME_SPEED, this.gameObject);
				Constants.CalculateGameConstants();
			}

			if ((Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
				&& Constants.GAME_SPEED > 1)
			{
				Constants.GAME_SPEED -= 1;
				Log("Decreasing GameSpeed: " + Constants.GAME_SPEED, this.gameObject);
				Constants.CalculateGameConstants();
			}

			if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
			{
				mapManager.InfluenceMap.gameObject.SetActive(!mapManager.InfluenceMap.gameObject.activeSelf);
			}
		}

		private void SetAllAgentsInactive()
		{
			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().Agent.gameObject.SetActive(false);
				agent.GetComponent<AgentController>().Agent.CloseLogFile();
			}
		}

		private void DeclareRoundWinner(GameObject winner)
		{
			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = true;

			AgentWins[winner.GetComponent<AgentController>().Agent.AgentName] += 1;
			winner.GetComponent<AgentController>().Agent.AgentNbrWins += 1;

			Prefabs.GameOverUI.GetComponentInChildren<Text>().text
				= winner.GetComponent<AgentController>().Agent.AgentName + " "
				  + winner.GetComponent<AgentController>().Agent.AgentDLLName + "\nWins Round";
			gameState = GameState.SHOWING_WINNER;

			if (winner.GetComponent<AgentController>().Agent.AgentName == Constants.HUMAN_ABBR)
			{
				Prefabs.HumanScoreText.text = AgentWins[Constants.HUMAN_ABBR].ToString();
			}
			else
			{
				Prefabs.OrcScoreText.text = AgentWins[Constants.ORC_ABBR].ToString();
			}
			TimeToDisplayBanner = 1.5f;
		}

        private void DisplaySingleAgentResults()
        {
	        string winnerAbbr = AgentWins[Constants.HUMAN_ABBR] >= AgentWins[Constants.ORC_ABBR]
				? Constants.HUMAN_ABBR
				: Constants.ORC_ABBR;

	        GameObject winner = null;
	        if (Agents[0].GetComponent<AgentController>().Agent.AgentName == winnerAbbr)
	        {
		        winner = Agents[0];
	        }
	        else
	        {
		        winner = Agents[1];
	        }

	        Prefabs.GameOverUI.GetComponentInChildren<Text>().text
		        = winner.GetComponent<AgentController>().Agent.AgentName
		          + " "+ winner.GetComponent<AgentController>().Agent.AgentDLLName
		          + "\nwon " + AgentWins[winnerAbbr] + " of " + TotalNbrOfRounds + "!";
        }

        private void DisplayMultiAgentResults()
        {
	        GameObject singleAgent = null;
	        int nbrWins = 0;

		    if (Agents[0].GetComponent<AgentController>().Agent.AgentName == Constants.ORC_ABBR)
		    {
			    singleAgent = Agents[1];
			    nbrWins = AgentWins[Constants.HUMAN_ABBR];
		    }
		    else
		    {
			    singleAgent = Agents[0];
			    nbrWins = AgentWins[Constants.HUMAN_ABBR];
		    }

	        Prefabs.GameOverUI.GetComponentInChildren<Text>().text
		        = singleAgent.GetComponent<AgentController>().Agent.AgentName + " "
	                  + singleAgent.GetComponent<AgentController>()
	                      .Agent.AgentDLLName + " won\n" + nbrWins +
	                  " of " + TotalNbrOfRounds + " rounds!";
        }

		#endregion

	}
}
