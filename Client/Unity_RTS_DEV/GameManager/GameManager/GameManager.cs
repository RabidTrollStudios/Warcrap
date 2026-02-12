using GameManager.EnumTypes;
using GameManager.GameElements;
using GameManager.Graph;
using Preloader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using System.Text.RegularExpressions;
using Object = System.Object;

namespace GameManager
{
	/// <summary>
	/// Manages the game
	/// </summary>
	public class GameManager : MonoBehaviour
	{
		#region Public GameObjects

		/// <summary>
		/// Name of the DLL to use for the Humans
		/// </summary>
		[Header("Player Settings")]

		[SerializeField] public string HumanDllName;

		/// <summary>
		/// Name of the DLL to use for the Orcs
		/// </summary>
		[SerializeField] public string OrcDllName;

		/// <summary>
		/// Should matches be played against random agents?
		/// </summary>
		[SerializeField] public bool RandomizeAgentsAsOrc;

		/// <summary>
		/// Starting gold for each player
		/// </summary>
		[Header("Game Settings")]

		[SerializeField] public int StartingPlayerGold = 1000;

		/// <summary>
		/// Amount of starting gold in each mine
		/// </summary>
		[SerializeField] public int StartingMineGold = 10000;

		/// <summary>
		/// Number of mines at the start of the game
		/// </summary>
		[SerializeField] public int NumberOfMines = 2;

		/// <summary>
		/// Starting Game Speed
		/// </summary>
		[SerializeField] public int StartingGameSpeed = 1;

		/// <summary>
		/// Number of competition rounds
		/// </summary>
		[SerializeField] public int TotalNbrOfRounds = 3;

		/// <summary>
		/// Maximum number of seconds a game may run
		/// </summary>
		[SerializeField] public int MaxNbrOfSeconds = 300;

		/// <summary>
		/// Color for the GM's log statements
		/// </summary>
		[SerializeField] private string GameManagerLogColor = "cyan";

		/// <summary>
		/// Time that has passed in the game, corrected for game-speed.
		/// </summary>
		[SerializeField] public float TotalGameTime = 0;

		/// <summary>
		/// Enable Learning for the Agents
		/// </summary>
		[SerializeField] public bool EnableLearning = true;

		/// <summary>
		/// Loader for all the game prefabs
		/// </summary>
		[Header("Prefabs")]
		[SerializeField] private PrefabLoader Prefabs;

		/// <summary>
		/// Human Debugger Canvas
		/// </summary>
		[SerializeField] private Canvas HumanDebuggerCanvas;

		/// <summary>
		/// Orc Debugger Canvas
		/// </summary>
		[SerializeField] private Canvas OrcDebuggerCanvas;

		#endregion

		#region Public Properties

		/// <summary>
		/// Instance of the game manager
		/// </summary>
		public static GameManager Instance => instance;

		/// <summary>
		/// Size of the map, +x is "right", +y is "up", z is ignored
		/// </summary>
		public Vector3Int MapSize { get; private set; }

		/// <summary>
		/// Turns the unit-specific debugging UIs on and off
		/// </summary>
		public bool HasUnitDebugging { get; private set; }

		/// <summary>
		/// Turns the agent debugging UIs on and off
		/// </summary>
		public bool HasAgentDebugging { get; private set; }

		/// <summary>
		/// The tilemap that renders the Influence Map on top of the game grid
		/// </summary>
		public Tilemap InfluenceMap { get; set; }

		#endregion

		#region Private Properties

		/// <summary>
		/// Path to the DLLs used for the Humans and the Orcs
		/// </summary>
		private string pathToDLLs = "";

		private enum GameState { PLAYING, SHOWING_WINNER, RESTARTING, FINISHED };

		private GameState gameState;

		/// <summary>
		/// 2D array of gridcells the size of the Map
		/// </summary>
		internal GridCell[,] GridCells { get; private set; }

		/// <summary>
		/// Collection of Units in the game
		/// </summary>
		private Dictionary<int, GameObject> Units { get; set; }

		/// <summary>
		/// Collection of Agents in the game
		/// </summary>
		private Dictionary<int, GameObject> Agents { get; set; }

		/// <summary>
		/// Number of wins per agent
		/// </summary>
		private Dictionary<string, int> AgentWins { get; set; }

		/// <summary>
		/// Graph used for pathfinding
		/// </summary>
		private Graph<GridCell> Graph { get => graph;
			set => graph = value;
		}

		/// <summary>
		/// Prefabs for orc player
		/// </summary>
		private Dictionary<UnitType, GameObject> OrcUnitPrefabs { get; set; }

		/// <summary>
		/// Prefabs for human player
		/// </summary>
		private Dictionary<UnitType, GameObject> HumanUnitPrefabs { get; set; }

		/// <summary>
		/// Collection of all prefabs
		/// </summary>
		private Dictionary<int, Dictionary<UnitType, GameObject>> UnitPrefabs { get; set; }

		/// <summary>
		/// Number of units created
		/// </summary>
		private int NbrOfUnits { get; set; }

		/// <summary>
		/// Number of agents created
		/// </summary>
		private int NbrOfAgents { get; set; }

		/// <summary>
		/// Time until we restart the game
		/// </summary>
		private float TimeToDisplayBanner { get; set; }

		#endregion

		#region Private Fields

		/// <summary>
		/// Singleton instance
		/// </summary>
		private static GameManager instance;

		/// <summary>
		/// Backing field for graph
		/// </summary>
		private Graph<GridCell> graph;

		/// <summary>
		/// Primary tilemap used to define the grid size
		/// </summary>
		private Tilemap mainTilemap;

		/// <summary>
		/// Number of rounds run so far
		/// </summary>
		private int NbrOfRounds;

		/// <summary>
		/// List of dllNames to pull from for the competition
		/// </summary>
		private List<string> dllNames = null;

		private bool isHumanUsingDllNames = false;

		private GameObject roundWinner = null;

		#endregion

		#region Initialization of Entire Game

		/// <summary>
		/// Constructor for GameManager - Singleton
		/// </summary>
		private GameManager()
		{
			if (instance != null && instance != this)
			{
				Destroy(gameObject);
			}
			else
			{
				instance = this;
			}
		}

		/// <summary>
		/// Initializes the Game Manager when it is instantiated
		/// </summary>
		private void Awake()
		{
			Constants.GAME_SPEED = StartingGameSpeed;
			Constants.CalculateGameConstants();

			OrcUnitPrefabs = new Dictionary<UnitType, GameObject>()
			{
				{ UnitType.MINE, Prefabs.MinePrefab },
				{ UnitType.WORKER, Prefabs.OrcPeonPrefab },
				{ UnitType.SOLDIER, Prefabs.OrcGruntPrefab },
				{ UnitType.ARCHER, Prefabs.OrcAxethrowerPrefab },
				{ UnitType.BASE, Prefabs.OrcBasePrefab },
				{ UnitType.BARRACKS, Prefabs.OrcBarracksPrefab },
				{ UnitType.REFINERY, Prefabs.OrcRefineryPrefab },
			};

			HumanUnitPrefabs = new Dictionary<UnitType, GameObject>()
			{
				{ UnitType.MINE, Prefabs.MinePrefab },
				{ UnitType.WORKER, Prefabs.HumanPeasantPrefab },
				{ UnitType.SOLDIER, Prefabs.HumanFootmanPrefab },
				{ UnitType.ARCHER, Prefabs.HumanArcherPrefab },
				{ UnitType.BASE, Prefabs.HumanBasePrefab },
				{ UnitType.BARRACKS, Prefabs.HumanBarracksPrefab },
				{ UnitType.REFINERY, Prefabs.HumanRefineryPrefab },
			};

			pathToDLLs = Application.dataPath + Path.AltDirectorySeparatorChar + ".."
				+ Path.AltDirectorySeparatorChar + ".." + Path.AltDirectorySeparatorChar
				+ "EnemyAgents";

			InitializeMatch();
		}

		#endregion

		#region Match Initialization

		IEnumerator DropIntroVersus()
		{
			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = true;

			yield return new WaitForSeconds(1.5f);

			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = false;
		}

		/// <summary>
		/// Get all of the names from the various PlanningAgent DLLs in the EnemyAgents folder
		/// </summary>
		void GetDLLNamesFromDir()
		{
			Log("NOTICE: Playing single round against all DLLs", this.gameObject);
			dllNames = new List<string>();

			string[] files = Directory.GetFiles(pathToDLLs);
			Debug.Log("pathToDLLs: " + pathToDLLs);
			Debug.Log("files: " + files.Length);
			Regex rx = new Regex(@"^.*PlanningAgent_(\w+)\.dll",
			  RegexOptions.Compiled | RegexOptions.IgnoreCase);
			foreach (string name in files)
			{
				Debug.Log("filename: " + name);
				MatchCollection matches = rx.Matches(name);

				if (matches.Count > 0 && matches[0].Groups.Count > 1 && matches[0].Groups[1].Value != "Mine")
				{
					// Pull out the dllname from the filename
					string dllName = matches[0].Groups[1].Value;
					dllNames.Add(dllName);
				}
			}

			Debug.Log("dllNames: " + dllNames.Count());
		}

		/// <summary>
		/// InitializeRound
		/// Called once at the beginning of each match (sequence of rounds)
		/// </summary>
		private void InitializeMatch()
		{
			if (RandomizeAgentsAsOrc)
			{
				OrcDllName = "";
				GetDLLNamesFromDir();

				if (dllNames.Count > 0)
				{
					OrcDllName = dllNames[Random.Range(0, dllNames.Count)];
					isHumanUsingDllNames = false;
				}
				else
				{
					GameManager.Instance.Log("ERROR: No DLLs to play against, exiting.", this.gameObject);
					Application.Quit();
				}
			}

			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = false;
			HasUnitDebugging = false;
			HasAgentDebugging = true;

			NbrOfAgents = 0;

			Graph = GenerateGraph();
			InfluenceMap.gameObject.SetActive(false);

			AgentWins = new Dictionary<string, int>();
			AgentWins[Constants.HUMAN_ABBR] = 0;
			AgentWins[Constants.ORC_ABBR] = 0;

			UnitPrefabs = new Dictionary<int, Dictionary<UnitType, GameObject>>();
			Agents = new Dictionary<int, GameObject>();
			Units = new Dictionary<int, GameObject>();

			// Randomly select one player to be instantiated first, for fairness
			if (UnityEngine.Random.Range(0, 2) == 0)
			{
				//GameManager.Instance.Log("Human Instantiated First");
				CreateAgent(Constants.HUMAN_ABBR, HumanDllName, Prefabs.HumanPlayerPrefab, HumanUnitPrefabs, HumanDebuggerCanvas);
				CreateAgent(Constants.ORC_ABBR, OrcDllName, Prefabs.OrcPlayerPrefab, OrcUnitPrefabs, OrcDebuggerCanvas);

			}
			else
			{
				//GameManager.Instance.Log("Orc Instantiated First");
				CreateAgent(Constants.ORC_ABBR, OrcDllName, Prefabs.OrcPlayerPrefab, OrcUnitPrefabs, OrcDebuggerCanvas);
				CreateAgent(Constants.HUMAN_ABBR, HumanDllName, Prefabs.HumanPlayerPrefab, HumanUnitPrefabs, HumanDebuggerCanvas);
			}

			Prefabs.HumanLabelText.text = Constants.HUMAN_ABBR + " " + HumanDllName;
			Prefabs.OrcLabelText.text = Constants.ORC_ABBR + " " + OrcDllName;

			Prefabs.GameOverUI.GetComponentInChildren<Text>().text
					= Prefabs.HumanLabelText.text + "\nvs\n" + Prefabs.OrcLabelText.text;

			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().InitializeMatch();
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
			// Create an agent
			GameObject agentObject = Instantiate(playerPrefab);
			agentObject.GetComponent<AgentController>().InitializeAgent(
				LoadDLL(agentName, agentDLLName), agentName, agentDLLName, NbrOfAgents++, debuggerCanvas, pathToDLLs);
			Agents.Add(agentObject.GetComponent<AgentController>().Agent.AgentNbr, agentObject);
			UnitPrefabs.Add(agentObject.GetComponent<AgentController>().Agent.AgentNbr, playerPrefabs);
		}

		private void RecreateAgent(string agentName, string agentDLLName, int agentNbr,
			GameObject playerPrefab, Dictionary<UnitType, GameObject> playerPrefabs, Canvas debuggerCanvas)
		{
			Destroy(Agents[agentNbr].GetComponent<AgentController>().Agent.gameObject);
			Destroy(Agents[agentNbr].GetComponent<AgentController>().gameObject);

			// Create an agent
			GameObject agentObject = Instantiate(playerPrefab);
			agentObject.GetComponent<AgentController>().InitializeAgent(
				LoadDLL(agentName, agentDLLName), agentName, agentDLLName, agentNbr, debuggerCanvas, pathToDLLs);
			Agents[agentNbr] = agentObject;
			UnitPrefabs[agentNbr] = playerPrefabs;
		}

		/// <summary>
		/// Load a dll for a PlanningAgent
		/// </summary>
		/// <param name="playerName"></param>
		/// <param name="dllName"></param>
		/// <returns></returns>
		private GameObject LoadDLL(string playerName, string dllName)
        {
            GameObject go = null;

			string filename = pathToDLLs + Path.AltDirectorySeparatorChar + "PlanningAgent_" + dllName + ".dll";
			Debug.Log("Opening dll file: " + filename);

			var DLL = Assembly.LoadFile(filename);
            if (DLL == null)
            {
				GameManager.Instance.Log("ERROR: Cannot find file: " + filename, this.gameObject);
            }

            foreach (Type type in DLL.GetExportedTypes())
            {
                go = new GameObject(type.Name, type);
                //DontDestroyOnLoad(go);
            }

            if (go == null)
            {
                GameManager.Instance.Log("ERROR: Cannot instantiate " + filename, this.gameObject);
            }
            return go;
        }

        #endregion

        #region Round Initialization

        /// <summary>
        /// InitializeRound
        /// Called once at the start of each round.  Multiple rounds
        /// make a match.
        /// </summary>
        private void InitializeRound()
		{
			Log("********************************** InitializeRound **********************************", gameObject);

			// Initialize the round
			gameState = GameState.PLAYING;
			TotalGameTime = 0;
			TimeToDisplayBanner = 0f;
			NbrOfUnits = 0;

			PickNextRandomAgent();

			NbrOfRounds++;

			foreach (GameObject agent in Agents.Values)
            {
	            agent.GetComponent<AgentController>().Agent.gameObject.SetActive(true);
				agent.GetComponent<AgentController>().Agent.OpenLogFile();
				agent.GetComponent<AgentController>().InitializeRound();
			}

			PlaceUnits();
		}

        private void PickNextRandomAgent()
        {
	        // If we're not on the first round and we are randomly selecting agents
	        if (NbrOfRounds > 0 && dllNames != null)
	        {
		        // Pick a random one
		        if (isHumanUsingDllNames)
		        {
			        int agentNbr = (Agents[0].GetComponent<AgentController>().Agent.AgentDLLName == HumanDllName) ? 0 : 1;
			        HumanDllName = dllNames[Random.Range(0, dllNames.Count)];
			        RecreateAgent(Constants.HUMAN_ABBR, HumanDllName, agentNbr, Prefabs.HumanPlayerPrefab, HumanUnitPrefabs,
				        HumanDebuggerCanvas);
			        Agents[agentNbr].GetComponent<AgentController>().InitializeMatch();
		        }
		        else
		        {
			        int agentNbr = (Agents[0].GetComponent<AgentController>().Agent.AgentDLLName == OrcDllName) ? 0 : 1;
			        OrcDllName = dllNames[Random.Range(0, dllNames.Count)];
			        RecreateAgent(Constants.ORC_ABBR, OrcDllName, agentNbr, Prefabs.OrcPlayerPrefab, OrcUnitPrefabs,
				        OrcDebuggerCanvas);
			        Agents[agentNbr].GetComponent<AgentController>().InitializeMatch();
		        }

		        //GameManager.Instance.Log("InitializeRound: " + HumanDLLName + " " + OrcDLLName);
		        Prefabs.HumanLabelText.text = Constants.HUMAN_ABBR + " " + HumanDllName;
		        Prefabs.OrcLabelText.text = Constants.ORC_ABBR + " " + OrcDllName;
		        //GameManager.Instance.Log("InitializeRound: " + Prefabs.HumanLabelText.text + " " + Prefabs.OrcLabelText.text);

		        Prefabs.GameOverUI.GetComponentInChildren<Text>().text
			        = Constants.HUMAN_ABBR + " " + HumanDllName + "\nvs\n" + Constants.ORC_ABBR + " " + OrcDllName;
		        StartCoroutine(DropIntroVersus());
	        }
        }

        private Vector3Int FindMirroredLocation(Vector3Int position, UnitType unitType)
        {
			return new Vector3Int(MapSize.x - Constants.UNIT_SIZE[unitType].x - position.x, 
								  MapSize.y - 2 + Constants.UNIT_SIZE[unitType].y - position.y, 0);
        }

		private void PlaceUnits()
        {
	        // Find a random location to place the first agent - make sure it is buildable
	        Vector3Int workerLocation = GetRandomBuildableLocation(UnitType.BASE);
	        Vector3Int mineLocation = GetRandomBuildableLocation(UnitType.MINE);
	        int initAgentNbr = UnityEngine.Random.Range(0, 2);

	        PlaceUnit(Agents[initAgentNbr], workerLocation, UnitType.WORKER, Color.white);
	        PlaceUnit(Agents[initAgentNbr], mineLocation, UnitType.MINE, Color.white);
	        PlaceUnit(Agents[(initAgentNbr + 1) % 2], FindMirroredLocation(workerLocation, UnitType.BASE), UnitType.WORKER, Color.white);
	        PlaceUnit(Agents[(initAgentNbr + 1) % 2], FindMirroredLocation(mineLocation, UnitType.BASE), UnitType.MINE, Color.white);
        }

        /// <summary>
        /// Learn
        /// Called after each round before any remaining units are destroyed so
        /// that the agent can observe the win state and learn from it
        /// </summary>
        private void Learn()
        {
			if (EnableLearning)
			{
				foreach (GameObject agent in Agents.Values)
				{
		            agent.GetComponent<AgentController>().Learn();
					agent.GetComponent<AgentController>().Agent.EndLogLine();
				}
			}
		}

        #endregion

        #region Graph Generation

        /// <summary>
        /// Generate the graph based on the tilemaps
        /// </summary>
        /// <returns>returns the generated graph</returns>
        private Graph<GridCell> GenerateGraph()
		{
			Graph = new Graph<GridCell>();

			// Find the larges bounds from all of the tilemaps
			MapSize = Vector3Int.zero;
			foreach (Tilemap tilemap in Prefabs.Grid.GetComponentsInChildren<Tilemap>())
			{
				tilemap.CompressBounds();

				if (tilemap.size.x > MapSize.x)
					MapSize = new Vector3Int(tilemap.size.x, MapSize.y, MapSize.z);
				if (tilemap.size.y > MapSize.y)
					MapSize = new Vector3Int(MapSize.x, tilemap.size.y, MapSize.z);
				if (tilemap.size.z > MapSize.z)
					MapSize = new Vector3Int(MapSize.x, MapSize.y, tilemap.size.z);
			}

			// If there are no tilemaps to process, produce an error
			if (Prefabs.Grid.GetComponentsInChildren<Tilemap>().Length == 0)
			{
				GameManager.Instance.Log("ERROR: no tilemaps", this.gameObject);
				return null;
			}
			// Otherwise, build a graph from the TileMap
			else
			{
				// Use the first tilemap as the map size and locations of tiles
				mainTilemap = Prefabs.Grid.GetComponentsInChildren<Tilemap>()[0];

				// Create the nodes
				GridCells = new GridCell[MapSize.x, MapSize.y];
				for (int i = 0; i < MapSize.x; ++i)
				{
					for (int j = 0; j < MapSize.y; ++j)
					{
						Vector3Int position = new Vector3Int(i, j, 0);
						GridCells[i, j] = new GridCell(mainTilemap, position);
						Graph.AddNode(Utility.GridToInt(position), GridCells[i, j]);
					}
				}

				// Build edges from all neighboring tiles
				GenerateEdges(ref graph);

				// Set all of the unbuildable nodes by iterating through the Tilemaps
				for (int t = 1; t < Prefabs.Grid.GetComponentsInChildren<Tilemap>().Length; ++t)
				{
					Tilemap tilemap = Prefabs.Grid.GetComponentsInChildren<Tilemap>()[t];

					if (tilemap.CompareTag("InfluenceMap"))
					{
						InfluenceMap = tilemap;
						continue;
					}

					for (int i = 0; i < MapSize.x; ++i)
					{
						for (int j = 0; j < MapSize.y; ++j)
						{
							Vector3Int position = new Vector3Int(i, j, 0);

							TileBase tile = tilemap.GetTile(position);
							if (tile != null)
							{
								GridCells[i, j].SetBuildable(false);
							}
						}
					}
				}
				return Graph;
			}
		}

		/// <summary>
		/// Generate all of the edges of the graph
		/// </summary>
		/// <param name="graph">the graph to which to add edges</param>
		private void GenerateEdges(ref Graph<GridCell> graph)
		{
			// Create the edges for all tiles in the map
			for (int i = 0; i < MapSize.x; ++i)
			{
				for (int j = 0; j < MapSize.y; ++j)
				{
					// For each of its neighbors
					for (int m = i - 1; m < i + 2; ++m)
					{
						for (int n = j - 1; n < j + 2; ++n)
						{
							// If this neighbor is "inside" the tilemap, add it as a neighbor
							if (m >= 0 && n >= 0 && m < MapSize.x && n < MapSize.y
								&& (i != m || j != n))
							{
								graph.AddEdge(Utility.GridToInt(new Vector3Int(i, j, 0)),
											  Utility.GridToInt(new Vector3Int(m, n, 0)),
											  Vector3.Distance(GridCells[i, j].Position, GridCells[m, n].Position));
							}
						}
					}
				}
			}

			// Calculate the cost between each pair of tiles to accelerate pathfinding
			graph.CalculateEstimatedCosts();
		}

        #endregion

        #region Unit Management

        /// <summary>
        /// Place a specific unit on a specific location
        /// </summary>
        /// <param name="agent">the agent that owns this new unit</param>
        /// <param name="gridPosition">location to place unit</param>
        /// <param name="unitType">type of unit to place</param>
        /// <param name="color">color associated with unit (used when more than 2 players)</param>
        /// <returns>the unit created</returns>
        internal GameObject PlaceUnit(GameObject agent, Vector3Int gridPosition, UnitType unitType, Color color)
		{
			// Compute the World position for the unit based on its size, unit assumes the position is in the center (for animation)
			Vector3 position = gridPosition + new Vector3((Constants.UNIT_SIZE[unitType].x - 1) * 0.5f,
								   -(Constants.UNIT_SIZE[unitType].y - 1) * 0.5f);

			// Instantiate this unit, add it to the list, and initialize it
			GameObject unit = Instantiate(UnitPrefabs[agent.GetComponent<AgentController>().Agent.AgentNbr][unitType], position, Quaternion.identity);
			unit.AddComponent<Unit>();
			unit.GetComponent<Unit>().Initialize(agent, gridPosition, unitType, NbrOfUnits++);

			// Add appropriate debugger info for this unit
			GameObject unitDebugger = Instantiate(Prefabs.UnitDebuggerPrefab, gridPosition, Quaternion.identity);
			unitDebugger.gameObject.GetComponentInChildren<Canvas>().enabled = false;
			unitDebugger.transform.SetParent(unit.transform);

			Units.Add(unit.GetComponent<Unit>().UnitNbr, unit);

			// Indicate that the tiles this unit is on are now not buildable
			SetAreaBuildability(unit.GetComponent<Unit>().UnitType, gridPosition, false);

			return unit;
		}

		/// <summary>
		/// Destroys a specific unit and clears its area
		/// </summary>
		/// <param name="unit">unit to destroy</param>
		internal void DestroyUnit(GameObject unit)
		{
			SetAreaBuildability(unit.GetComponent<Unit>().UnitType, unit.GetComponent<Unit>().GridPosition, true);
			Units.Remove(unit.GetComponent<Unit>().UnitNbr);
			Destroy(unit);
		}

		/// <summary>
		///  Set the unit's current cell(s) to buildable or not
		/// </summary>
		/// <param name="unitType">unit type that has been built</param>
		/// <param name="gridPosition">position of this unit (upper left corner)</param>
		/// <param name="isBuildable">new value of IsBuildable</param>
		internal void SetAreaBuildability(UnitType unitType, Vector3Int gridPosition, bool isBuildable)
		{
			Vector3Int gridPos = Vector3Int.zero;
			Vector3Int size = Constants.UNIT_SIZE[unitType];

			// For all of the tiles this unit consumes, set them to isBuildable
			for (int i = 0; i < size.x; ++i)
			{
				for (int j = 0; j < size.y; ++j)
				{
					gridPos = gridPosition + new Vector3Int(i, -j, 0);

					if (Utility.IsValidGridLocation(gridPos) &&
						IsGridPositionBuildable(gridPos) != isBuildable)
					{
						GridCells[gridPos.x, gridPos.y].SetBuildable(isBuildable);
					}
				}
			}
		}

		#endregion

		#region Event Handlers

		/// <summary>
		/// Log message that colorizes all debug statements from this package
		/// </summary>
		/// <param name="message"></param>
		/// <param name="context"></param>
		#line hidden
		internal void Log(string message, GameObject context)
		{
			Debug.Log("<color=" + GameManagerLogColor + ">" + message + "</color>", context);
		}
		#line default

		/// <summary>
		/// Initializes a move of a specific unit
		/// </summary>
		/// <param name="sender">agent who is sending this command</param>
		/// <param name="e">parameters of this command</param>
		internal void MoveEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			MoveEventArgs args = (MoveEventArgs)e;

			// Check that the arguments are not null
			if (agent == null || args.Unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to move event", this.gameObject);
				return;
			}

			// Check that the Unit belongs to the agent
			GameObject unit = Units[args.Unit.UnitNbr];
			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", this.gameObject);
				return;
			}

			// Check that the unit can move
			if (!unit.GetComponent<Unit>().CanMove)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit cannot move " + unit.GetComponent<Unit>().UnitType, this.gameObject);
				return;
			}

			// Check that the target location is valid
			if (!Utility.IsValidGridLocation(args.Target))
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: invalid target grid position " + args.Target, this.gameObject);
				return;
			}

			// Start moving the unit
			unit.GetComponent<Unit>().StartMoving(args);
		}

		/// <summary>
		/// Initializes a build of a specific unit
		/// </summary>
		/// <param name="sender">agent who is sending this command</param>
		/// <param name="e">parameters of this command</param>
		internal void BuildEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			BuildEventArgs args = (BuildEventArgs)e;

			// Check that the args aren't null
			if (agent == null || args.Unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to build event", this.gameObject);
				return;
			}

			// Check that the Unit belongs to the agent
			GameObject unit = Units[args.Unit.UnitNbr];
			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", this.gameObject);
				return;
			}

			// Check that the unit can build
			if (!unit.GetComponent<Unit>().CanBuild
				|| !Constants.BUILDS[unit.GetComponent<Unit>().UnitType].Contains(args.UnitType))
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit cannot build " + args.UnitType, this.gameObject);
				return;
			}

			// Check that the target location is valid
			if (!Utility.IsValidGridLocation(args.TargetPosition) || !IsAreaBuildable(args.UnitType, args.TargetPosition))
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: invalid target grid position " + args.TargetPosition, this.gameObject);
				return;
			}

			// Check that the dependencies are satisfied
			bool hasDependencies = true;
			string dependencyName = "";
			foreach (UnitType uT in Constants.DEPENDENCY[args.UnitType])
			{
				// If this unit type doesn't exist in this agent's current units
				if (GameManager.Instance.GetUnitNbrsOfType(uT).Where(
						u => GameManager.Instance.GetUnit(u).IsBuilt).ToList().Count == 0)
				{
					hasDependencies = false;
					dependencyName += uT + " ";
				}
			}

			if (!hasDependencies)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: Missing dependencies " + dependencyName + "for building " + args.UnitType, this.gameObject);
				return;
			}
			unit.GetComponent<Unit>().StartBuilding(args);
		}

		/// <summary>
		/// Initializes a gather of a specific resource
		/// </summary>
		/// <param name="sender">agent who is sending this command</param>
		/// <param name="e">parameters of this command</param>
		internal void GatherEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			GatherEventArgs args = (GatherEventArgs)e;

			// Check all of the arguments for null before continuing
			if (agent == null || args.Unit == null || args.BaseUnit == null || args.ResourceUnit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to gather event", this.gameObject);
				return;
			}

			// Check that this agent is the current agent
			GameObject unit = Units[args.Unit.UnitNbr];
			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", this.gameObject);
				return;
			}

			// Check that the unit can build
			if (!unit.GetComponent<Unit>().CanGather)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit cannot gather " + args.Unit.UnitType, this.gameObject);
				return;
			}

			// Check that the resource unit is valid
			if (args.ResourceUnit.UnitType != UnitType.MINE)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: cannot gather from  " + args.ResourceUnit.UnitType, this.gameObject);
				return;
			}

			// Check that the base unit is valid
			if (args.BaseUnit.UnitType != UnitType.BASE
				|| args.BaseUnit.Agent.GetComponent<AgentController>().Agent.AgentNbr != agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: cannot return resources to  " + args.BaseUnit.UnitType, this.gameObject);
				return;
			}

			unit.GetComponent<Unit>().StartGathering(args);
		}

		/// <summary>
		/// Initializes a training of a specific unit
		/// </summary>
		/// <param name="sender">agent who is sending this command</param>
		/// <param name="e">parameters of this command</param>
        internal void TrainEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			TrainEventArgs args = (TrainEventArgs)e;

			if (agent == null || args.Unit == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to train event", this.gameObject);
				return;
			}

			// Check that the Unit belongs to the agent
			GameObject unit = Units[args.Unit.UnitNbr];
			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", this.gameObject);
				return;
			}

			// Check that the unit can train this unit
			if (!unit.GetComponent<Unit>().CanTrain
				|| !unit.GetComponent<Unit>().CanTrainUnit(args.UnitType))
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: unit cannot train " + args.UnitType, this.gameObject);
				return;
			}

			unit.GetComponent<Unit>().StartTraining(args);
		}

		/// <summary>
		/// Initializes an attack of a specific unit
		/// </summary>
		/// <param name="sender">agent who is sending this command</param>
		/// <param name="e">parameters of this command</param>
        internal void AttackEventHandler(object sender, EventArgs e)
		{
			Agent agent = (Agent)sender;
			AttackEventArgs args = (AttackEventArgs)e;

			if (agent == null || args.Unit == null || args.Target == null)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: null parameters to attack event", this.gameObject);
				return;
			}

			// Check that the Unit belongs to the agent
			GameObject unit = Units[args.Unit.UnitNbr];
			if (agent.AgentNbr != unit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " CHEATER attempting to send events for the other agent", this.gameObject);
				return;
			}

			// Check that the Unit belongs to the agent
			GameObject enemyUnit = Units[args.Target.UnitNbr];
			if (agent.AgentNbr == enemyUnit.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr)
			{
				GameManager.Instance.Log(agent.AgentName + " ERROR: agent can't attack its own units", this.gameObject);
				return;
			}

			unit.GetComponent<Unit>().StartAttacking(args);
		}

		#endregion

		#region Public Unit Access Methods

		/// <summary>
		/// Get the agent by their agent number
		/// </summary>
		/// <param name="agentNbr"></param>
		/// <returns></returns>
		public Agent GetAgent(int agentNbr)
		{
			return Agents[agentNbr].GetComponent<AgentController>().Agent;
		}

		/// <summary>
		/// Gets my enemy agent numbers
		/// </summary>
		/// <returns>list of unit numbers</returns>
		public List<int> GetEnemyAgentNbrs(int agentNbr)
		{
			return Agents.Keys.Where(key => key != agentNbr).ToList();
		}

		/// <summary>
		/// Gets a list of units of the given type
		/// </summary>
		/// <param name="unitType"></param>
		/// <returns>list of unit numbers</returns>
		public List<int> GetUnitNbrsOfType(UnitType unitType)
		{
			return Units.Keys.Where(key => Units[key].GetComponent<Unit>().UnitType == unitType).ToList();
		}

		/// <summary>
		/// Gets a list of units of the given type for the given agent
		/// </summary>
		/// <param name="unitType">type of unit to get</param>
		/// <param name="agentNbr">number of the agent</param>
		/// <returns>list of unit numbers</returns>
		public List<int> GetUnitNbrsOfType(UnitType unitType, int agentNbr)
		{
			return Units.Keys.Where(key => Units[key].GetComponent<Unit>().UnitType == unitType
								&& Units[key].GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr == agentNbr).ToList();
		}

		/// <summary>
		/// Get a specific unit based on its unit number
		/// </summary>
		/// <param name="unitNbr">unique unit number to find</param>
		/// <returns>unit or null if the unit no longer exists</returns>
		public Unit GetUnit(int unitNbr)
		{
			if (Units.ContainsKey(unitNbr))
			{ return Units[unitNbr].GetComponent<Unit>(); }
			else
			{ return null; }
		}


		#endregion

		#region Public Graph Searching Methods

		/// <summary>
		/// Determines if a specific tile is buildable
		/// </summary>
		/// <param name="position">position of the tile in the tilemap</param>
		/// <returns>true if buildable, false otherwise</returns>
		public bool IsGridPositionBuildable(Vector3Int position)
		{
			return GridCells[position.x, position.y].IsBuildable();
		}

		/// <summary>
		/// Determines if the unit can be built in that area (base on size of unit)
		/// </summary>
		/// <param name="unitType">unit type to build, determines size of area to test</param>
		/// <param name="gridPosition">upper-left tile of this unit</param>
		/// <returns>true if area is buildable, false otherwise</returns>
		public bool IsAreaBuildable(UnitType unitType, Vector3Int gridPosition)
		{
			Vector3Int gridPos = Vector3Int.zero;
			Vector3Int size = Constants.UNIT_SIZE[unitType];

			// For all of the tiles this unit consumes, set them to isBuildable
			for (int i = 0; i < size.x; ++i)
			{
				for (int j = 0; j < size.y; ++j)
				{
					gridPos = gridPosition + new Vector3Int(i, -j, 0);

					// If the position is out of bounds OR not buildable
					if (!Utility.IsValidGridLocation(gridPos)
						|| !IsGridPositionBuildable(gridPos))
					{
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>
		/// Determines if the unit can be built in that area with a walkable "boundary" around it.
		/// This should be used to place units (like a barracks) that need space around them for new
		/// units to be created or for units to stand.
		/// </summary>
		/// <param name="unitType">unit type to build</param>
		/// <param name="gridPosition">upper-left tile of this unit</param>
		/// <returns>true if area is buildable, false otherwise</returns>
		public bool IsBoundedAreaBuildable(UnitType unitType, Vector3Int gridPosition)
		{
			Vector3Int gridPos = Vector3Int.zero;
			Vector3Int size = Constants.UNIT_SIZE[unitType];

			// For all of the tiles this unit consumes, set them to isBuildable
			for (int i = -1; i <= size.x; ++i)
			{
				for (int j = -1; j <= size.y; ++j)
				{
					gridPos = gridPosition + new Vector3Int(i, -j, 0);

					// If the position is out of bounds OR not buildable
					if (!Utility.IsValidGridLocation(gridPos)
						|| !IsGridPositionBuildable(gridPos))
					{
						return false;
					}
				}
			}
			return true;
		}

        /// <summary>
        /// Determines if the gridPosition is a neighbor of the unit
        /// </summary>
        /// <param name="gridPosition">gridPosition to test</param>
        /// <param name="unitType">type of unit</param>
        /// <param name="unitGridPosition">unit's grid position</param>
        /// <returns>true if gridPosition is a neighbor of unit, false otherwise</returns>
        public bool IsNeighborOfUnit(Vector3Int gridPosition, UnitType unitType, Vector3Int unitGridPosition)
        {
            var neighbors = GameManager.Instance.GetGridPositionsNearUnit(unitType, unitGridPosition);

            return neighbors.Contains(gridPosition);
        }

		/// <summary>
        /// Get all of the grid positions surrounding a particular unit
        /// </summary>
        /// <param name="unitType">type of unit</param>
        /// <param name="gridPosition">upper-left-hand cell at which to build unit</param>
        /// <returns>list of positions near a unit</returns>
        public List<Vector3Int> GetGridPositionsNearUnit(UnitType unitType, Vector3Int gridPosition)
		{
			Vector3Int gridPos;
			List<Vector3Int> positions = new List<Vector3Int>();

			// Check the rows above & below the unit
			for (int i = gridPosition.x - 1; i <= gridPosition.x + Constants.UNIT_SIZE[unitType].x; ++i)
			{
				// Check the top row
				gridPos = new Vector3Int(i, gridPosition.y + 1, 0);
				if (Utility.IsValidGridLocation(gridPos))
					positions.Add(gridPos);

				// Check the bottom row
				gridPos = new Vector3Int(i, gridPosition.y - Constants.UNIT_SIZE[unitType].y, 0);
				if (Utility.IsValidGridLocation(gridPos))
					positions.Add(gridPos);
			}

			// Check the columns to the left and right of unit
			for (int j = gridPosition.y - Constants.UNIT_SIZE[unitType].y + 1; j <= gridPosition.y; ++j)
			{
				// Check the top row
				gridPos = new Vector3Int(gridPosition.x - 1, j, 0);
				if (Utility.IsValidGridLocation(gridPos))
					positions.Add(gridPos);

				// Check the bottom row
				gridPos = new Vector3Int(gridPosition.x + Constants.UNIT_SIZE[unitType].x, j, 0);
				if (Utility.IsValidGridLocation(gridPos))
					positions.Add(gridPos);
			}

			return positions;
		}

		/// <summary>
		/// Find all of the buildable grid positions near this unit
		/// </summary>
		/// <param name="unitType">type of unit to build</param>
		/// <param name="gridPosition">upper-left-hand cell at which to build unit</param>
		/// <returns>list of positions that are buildable near unit</returns>
		public List<Vector3Int> GetBuildableGridPositionsNearUnit(UnitType unitType, Vector3Int gridPosition)
		{
			List<Vector3Int> positions = GetGridPositionsNearUnit(unitType, gridPosition);
			return positions.Where(IsGridPositionBuildable).ToList();
		}

        /// <summary>
        /// Find a random location that is buildable for the unit type provided
        /// </summary>
        /// <returns>a buildable position somewhere in the map</returns>
        public Vector3Int GetRandomBuildableLocation(UnitType unitType)
        {
            // Find a random location to place - make sure it is buildable
            Vector3Int location = Vector3Int.zero;

            do
            {
                location = new Vector3Int(UnityEngine.Random.Range(1, 72), UnityEngine.Random.Range(1, 41), 0);
            } while (!IsAreaBuildable(unitType, location));

            return location;
        }

		/// <summary>
		/// Get the path from a gridPosition to a position near the unit on any side of it
		/// </summary>
		/// <param name="gridPosition">starting grid position for the unit that will be moving</param>
		/// <param name="unitType">type of the target unit</param>
		/// <param name="unitGridPosition">gridPosition of the target unit</param>
		/// <returns>list of gridPositions the unit will need to traverse in the valid path</returns>
		public List<Vector3Int> GetPathToUnit(Vector3Int gridPosition, UnitType unitType, Vector3Int unitGridPosition)
		{
			List<Vector3Int> path = new List<Vector3Int>();
			List<Vector3Int> positions = GameManager.Instance.GetBuildableGridPositionsNearUnit(unitType, unitGridPosition);

			// Find the first position that has a valid path
			foreach (var position in positions)
			{
				path = GetPathBetweenGridPositions(gridPosition, position);
				if (path.Count > 0)
				{
					return path;
				}
			}
			return path;
		}

		/// <summary>
		/// Gets the path between two grid positions
		/// </summary>
		/// <param name="startGridPosition">starting position</param>
		/// <param name="endGridPosition">ending position</param>
		/// <returns>list of positions from start to end, empty if no path was found</returns>
		public List<Vector3Int> GetPathBetweenGridPositions(Vector3Int startGridPosition, Vector3Int endGridPosition)
		{
			List<Vector3Int> path = new List<Vector3Int>();

			// Determine path to the unitGridPosition
			int start = Utility.GridToInt(startGridPosition);
			int end = Utility.GridToInt(endGridPosition);

			List<int> pathOfInts = GameManager.Instance.Graph.AStarSearch(start, end);

			// Convert each gridCellNbr to a position
			foreach (var nodeNbr in pathOfInts)
			{
				path.Add(Utility.IntToGrid(nodeNbr));
			}

			return path;
		}

		#endregion

		#region Game Management Methods

		GameObject CalcScorePerUnit(Dictionary<GameObject, List<GameObject>> agentUnits,
			Dictionary<GameObject, int> agentScore)
		{
			foreach (GameObject agent in Agents.Values)
			{
				agentScore.Add(agent, 0);
				foreach(UnitType unitType in Enum.GetValues(typeof(UnitType)))
				{
					int value = agentUnits[agent].Count(unit => unit.GetComponent<Unit>().UnitType == unitType)
					            * Constants.UNIT_VALUE[unitType];
					agentScore[agent] += value;
					//GameManager.Instance.Log("UnitValue: " + agent.GetComponent<AgentController>().Agent.AgentName + " " + unitType + " " + value, this.gameObject);
				}
			}
			//GameManager.Instance.Log("Agent " + Agents[0].GetComponent<AgentController>().Agent.AgentDLLName + ": " + agentScore[Agents[0]]);
			//GameManager.Instance.Log("Agent " + Agents[1].GetComponent<AgentController>().Agent.AgentDLLName + ": " + agentScore[Agents[1]]);

			if (agentScore[Agents[0]] > agentScore[Agents[1]])
			{
				return Agents[0];
			}
			else if (agentScore[Agents[0]] < agentScore[Agents[1]])
			{
				return Agents[1];
			}
			else if (Agents[0].GetComponent<AgentController>().Agent.GetComponent<Agent>().Gold 
				> Agents[1].GetComponent<AgentController>().Agent.GetComponent<Agent>().Gold)
			{
				return Agents[0];
			}
			else
			{
				return Agents[1];
			}
		}

        /// <summary>
        /// Determines if there is a game winner or not
        /// </summary>
        /// <returns>winning agent, null otherwise</returns>
        private GameObject DetermineRoundWinner()
		{
			int countActiveAgents = 0;
			Dictionary<GameObject, List<GameObject>> agentUnits = new Dictionary<GameObject, List<GameObject>>();
			Dictionary<GameObject, int> agentScore = new Dictionary<GameObject, int>();

			// Identify all Units per agent (except mines)
			foreach (GameObject agent in Agents.Values)
			{
				agentUnits.Add(agent, Units.Values.Where(
                    y => (y.GetComponent<Unit>().Agent.GetComponent<AgentController>().Agent.AgentNbr
							== agent.GetComponent<AgentController>().Agent.AgentNbr 
						  && y.GetComponent<Unit>().UnitType != UnitType.MINE)).ToList());
				if (agentUnits[agent].Count > 0)
				{
					++countActiveAgents;
				}
			}

			// If the game has gone on for more than the maximum allowed number of seconds, declare a winner
			if (TotalGameTime > MaxNbrOfSeconds)
			{
				return CalcScorePerUnit(agentUnits, agentScore);
			}	

			// If there are more than one active agent (i.e. have more than 0 Units remaining), no winner yet
			if (countActiveAgents > 1)
			{
				return null;
			}

			// Determine the remaining agent
			foreach (GameObject agent in agentUnits.Keys)
			{
				if (agentUnits[agent].Count > 0)
				{
                    return agent;
				}
			}

			return null;
		}

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        internal void Update()
        {
			Log("********************************** GameManager Update **********************************", this.gameObject);

			// Normal game progress
			if (gameState == GameState.PLAYING)
			{
				UpdateTimerUI();
				ProcessUserInput();

				// If we just found the winner, display the winner
				roundWinner = DetermineRoundWinner();
				if (roundWinner != null)
				{
					DeclareRoundWinner(roundWinner);
					Learn();
					SetAllUnitsInactive();
					gameState = GameState.SHOWING_WINNER;
				}
			}           
			// Display the winner for about 1.5 seconds
			else if (gameState == GameState.SHOWING_WINNER)
            {
				// Setup timer to wait for 1 second and then restart
				TimeToDisplayBanner -= Time.deltaTime; // * Constants.GAME_SPEED;
                if (TimeToDisplayBanner < 0.0)
                {
					DestroyAllUnits();

					int sum = DetermineRoundsCompleted();

					// If we've exhausted all of our rounds, declare an overall winner
	                if (sum == TotalNbrOfRounds)
					{
						TimeToDisplayBanner = 1.5f; // * Constants.GAME_SPEED;
						gameState = GameState.FINISHED;
					}
					else
	                {
		                gameState = GameState.RESTARTING;
	                }
				}
			}
            // Restart the game
            else if (gameState == GameState.RESTARTING)
            {
                Prefabs.GameOverUI.GetComponent<Canvas>().enabled = false;
                InitializeRound();
            }
			else if (gameState == GameState.FINISHED)
			{
				// If this is a one-on-one game
				if (dllNames == null)
				{
					DisplaySingleAgentResults();
				}
				// Otherwise, this is a multi-agent set of rounds
				else
				{
					DisplayMultiAgentResults();
				}

				Debug.Break();
				return;
			}
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

		    // Determine which agent is the multi-agent
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

        private int DetermineRoundsCompleted()
        {
	        int sum = 0;

	        foreach (string agentName in AgentWins.Keys)
	        {
		        sum += AgentWins[agentName];
	        }

	        return sum;
        }

        private void UpdateTimerUI()
		{
			// update the TotalGameTime, scaled by gamespeed
			TotalGameTime += Time.deltaTime * Constants.GAME_SPEED;

			// Update the timerUI
			Prefabs.TimerText.text = TotalGameTime.ToString("0.00000");

			// Update the speed
			Prefabs.SpeedText.text = Constants.GAME_SPEED.ToString();
		}

		private void ProcessUserInput()
		{
			// Turn the Agent debugging canvas on and off
			if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
			{
				GameManager.Instance.Log("Setting Agent Debugging: " + HasAgentDebugging, this.gameObject);
				HasAgentDebugging = !HasAgentDebugging;
			}

			// Turn the unit debugging canvases on and off
			if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
			{
				GameManager.Instance.Log("Setting Unit Debugging: " + HasUnitDebugging, this.gameObject);
				HasUnitDebugging = !HasUnitDebugging;
			}

			// Increase game speed
			if ((Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus)
												  || Input.GetKeyDown(KeyCode.KeypadPlus))
				&& Constants.GAME_SPEED < Constants.MAX_GAME_SPEED)
			{
				Constants.GAME_SPEED += 1;
				GameManager.Instance.Log("Increasing GameSpeed: " + Constants.GAME_SPEED, this.gameObject);
				Constants.CalculateGameConstants();
			}

			// Decrease game speed
			if ((Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
				&& Constants.GAME_SPEED > 1)
			{
				Constants.GAME_SPEED -= 1;
				GameManager.Instance.Log("Decreasing GameSpeed: " + Constants.GAME_SPEED, this.gameObject);
				Constants.CalculateGameConstants();
			}

			// Enable or disable the InfluenceMap
			if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
			{
				InfluenceMap.gameObject.SetActive(!InfluenceMap.gameObject.activeSelf);
			}
		}

		private void SetAllUnitsInactive()
		{
			foreach (GameObject agent in Agents.Values)
			{
				agent.GetComponent<AgentController>().Agent.gameObject.SetActive(false);
				agent.GetComponent<AgentController>().Agent.CloseLogFile();
			}

			// Destroy all of the old units
			List<int> unitNbrs = Units.Keys.ToList();

			foreach (var unitNbr in unitNbrs)
			{
				if (Units.ContainsKey(unitNbr))
				{
					Units[unitNbr].SetActive(false);
				}
			}
		}

		private void DestroyAllUnits()
		{
			// Destroy all of the old units
			List<int> unitNbrs = Units.Keys.ToList();

			foreach (var unitNbr in unitNbrs)
			{
				if (Units.ContainsKey(unitNbr))
				{
					DestroyUnit(Units[unitNbr]);
				}
			}
			Units = new Dictionary<int, GameObject>();
		}

		private void DeclareRoundWinner(GameObject winner)
		{
			Prefabs.GameOverUI.GetComponent<Canvas>().enabled = true;

			AgentWins[winner.GetComponent<AgentController>().Agent.AgentName] += 1;
			winner.GetComponent<AgentController>().Agent.AgentNbrWins += 1;
			//GameManager.Instance.Log("AgentWins[winner.GetComponent<AgentController>().Agent.AgentName]: " + AgentWins[winner.GetComponent<AgentController>().Agent.AgentName]);
			//GameManager.Instance.Log("TotalNbrOfRounds: " + TotalNbrOfRounds);

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
			TimeToDisplayBanner = 1.5f; // * Constants.GAME_SPEED;
		}

		#endregion

	}
}