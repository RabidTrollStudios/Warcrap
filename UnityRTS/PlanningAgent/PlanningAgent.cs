using System.Collections.Generic;
using System.Linq;
using GameManager.EnumTypes;
using GameManager.GameElements;
using UnityEngine;
using System;

/////////////////////////////////////////////////////////////////////////////
// This is the Moron Agent
/////////////////////////////////////////////////////////////////////////////

namespace GameManager
{
    ///<summary>Planning Agent is the over-head planner that decided where
    /// individual units go and what tasks they perform.  Low-level 
    /// AI is handled by other classes (like pathfinding).
    ///</summary> 
    public class PlanningAgent : Agent
    {
        private const int MAX_NBR_WORKERS = 10;
        private const int MAX_NBR_BASES = 2;
        private const int MAX_NBR_BARRACKS = 5;
        private const int MAX_NBR_REFINERIES = 2;
        private const int MAX_NBR_SOLDIERS = 40;
        private const int MAX_NBR_ARCHERS = 10;

        private const int MAX_NBR_OF_UNIT_TO_ATTACK = 5;

        #region Private Data

        ///////////////////////////////////////////////////////////////////////
        // Handy short-cuts for pulling all of the relevant data that you
        // might use for each decision.  Feel free to add your own.
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The enemy's agent number
        /// </summary>
        private int enemyAgentNbr { get; set; }

        /// <summary>
        /// My primary mine number
        /// </summary>
        private int mainMineNbr { get; set; }
        private int secondMineNbr = -1;

        /// <summary>
        /// My primary base number
        /// </summary>
        private int mainBaseNbr { get; set; }
        private int secondaryBaseNbr { get; set; }

        /// <summary>
        /// List of all the mines on the map
        /// </summary>
        private List<int> mines { get; set; }

        /// <summary>
        /// List of all of my workers
        /// </summary>
        private List<int> myWorkers { get; set; }

        /// <summary>
        /// List of all of my soldiers
        /// </summary>
        private List<int> mySoldiers { get; set; }

        /// <summary>
        /// List of all of my archers
        /// </summary>
        private List<int> myArchers { get; set; }

        /// <summary>
        /// List of all of my bases
        /// </summary>
        private List<int> myBases { get; set; }

        /// <summary>
        /// List of all of my barracks
        /// </summary>
        private List<int> myBarracks { get; set; }

        /// <summary>
        /// List of all of my refineries
        /// </summary>
        private List<int> myRefineries { get; set; }

        /// <summary>
        /// List of the enemy's workers
        /// </summary>
        private List<int> enemyWorkers { get; set; }

        /// <summary>
        /// List of the enemy's soldiers
        /// </summary>
        private List<int> enemySoldiers { get; set; }

        /// <summary>
        /// List of enemy's archers
        /// </summary>
        private List<int> enemyArchers { get; set; }

        /// <summary>
        /// List of the enemy's bases
        /// </summary>
        private List<int> enemyBases { get; set; }

        /// <summary>
        /// List of the enemy's barracks
        /// </summary>
        private List<int> enemyBarracks { get; set; }

        /// <summary>
        /// List of the enemy's refineries
        /// </summary>
        private List<int> enemyRefineries { get; set; }

        /// <summary>
        /// List of the possible build positions for a 3x3 unit
        /// </summary>
        private List<Vector3Int> buildPositions { get; set; }

        private enum GameState
        {
            BaseBuilding,
            ArmyBuilding,
            Attacking
        }

        private GameState currState = GameState.BaseBuilding;

        private UnitType unitTypeToBuildAround;
        private Vector3Int unitToBuildAroundPos = Vector3Int.zero;

        private Vector3Int buildPos = Vector3Int.zero;

        /// <summary>
        /// Finds all of the possible build locations for a specific UnitType.
        /// Currently, all structures are 3x3, so these positions can be reused
        /// for all structures (Base, Barracks, Refinery)
        /// Run this once at the beginning of the game and have a list of
        /// locations that you can use to reduce later computation.  When you
        /// need a location for a build-site, simply pull one off of this list,
        /// determine if it is still buildable, determine if you want to use it
        /// (perhaps it is too far away or too close or not close enough to a mine),
        /// and then simply remove it from the list and build on it!
        /// This method is called from the Awake() method to run only once at the
        /// beginning of the game.
        /// </summary>
        /// <param name="unitType">the type of unit you want to build</param>
        public void FindProspectiveBuildPositions(UnitType unitType)
        {
            // For the entire map
            for (int i = 0; i < GameManager.Instance.MapSize.x; ++i)
            {
                for (int j = 0; j < GameManager.Instance.MapSize.y; ++j)
                {
                    // Construct a new point near gridPosition
                    Vector3Int testGridPosition = new Vector3Int(i, j, 0);

                    // Test if that position can be used to build the unit
                    if (Utility.IsValidGridLocation(testGridPosition)
                        && GameManager.Instance.IsBoundedAreaBuildable(unitType, testGridPosition))
                    {
                        // If this position is buildable, add it to the list
                        buildPositions.Add(testGridPosition);
                    }
                }
            }
        }

        /// <summary>
        /// Build a building
        /// </summary>
        /// <param name="unitType"></param>
        public void BuildBuilding(UnitType unitType)
        {
            // For each worker
            foreach (int worker in myWorkers)
            {
                // Grab the unit we need for this function
                Unit unit = GameManager.Instance.GetUnit(worker);

                // Make sure this unit actually exists & they're only gathering gold
                if (unit != null && (unit.CurrentAction == UnitAction.GATHER || unit.CurrentAction == UnitAction.IDLE))
                {
                    Vector3Int unitPos = Vector3Int.RoundToInt(unit.transform.position);
                    float dist = Vector3Int.Distance(unitPos, buildPos);

                    // IF we're trying to build a base, find the closest possible space to it
                    if (unitType == UnitType.BASE)
                    {
                        // Save list of all buildable grid positions near where we want to build
                        List<Vector3Int> buildablePositions = GameManager.Instance.GetBuildableGridPositionsNearUnit
                            (unitTypeToBuildAround, unitToBuildAroundPos);

                        buildPos = buildablePositions[0];

                        // Find the closest buildable grid position to the current unit
                        for (int i = 0; i < buildablePositions.Count; i++)
                        {
                            Vector3Int checkingBuildPos = buildablePositions[i];
                            float checkingDist = Vector3Int.Distance(unitPos, checkingBuildPos);

                            if (checkingDist < dist)
                            {
                                // Check if the buildable grid position is a buildable area
                                if (GameManager.Instance.IsBoundedAreaBuildable(unitType, checkingBuildPos))
                                {
                                    dist = checkingDist;
                                    buildPos = buildablePositions[i];
                                }
                            }
                        }
                    }

                    // If the area chosen above isn't buildable OR we're not building a base, choose a new build location near where we want to build
                    if (!GameManager.Instance.IsBoundedAreaBuildable(unitType, buildPos) || unitType != UnitType.BASE)
                    {
                        for (int i = 0; i < buildPositions.Count; i++)
                        {
                            // Check if the current build position is closer to the unit
                            if (Vector3Int.Distance(unitToBuildAroundPos, buildPositions[i]) < dist)
                            {
                                // Check if the build position is a buildable area
                                if (GameManager.Instance.IsBoundedAreaBuildable(unitType, buildPositions[i]))
                                {
                                    dist = Vector3Int.Distance(unitToBuildAroundPos, buildPositions[i]);
                                    buildPos = buildPositions[i];
                                }
                            }
                        }
                    }

                    Build(unit, buildPos, unitType);
                    break;
                }
            }
        }

        /// <summary>
        /// Attack the enemy
        /// </summary>
        /// <param name="myTroops"></param>
        public void AttackEnemy(List<int> myTroops)
        {
            // For each of my troops in this collection
            foreach (int troopNbr in myTroops)
            {
                // If this troop is idle, give him something to attack
                Unit troopUnit = GameManager.Instance.GetUnit(troopNbr);
                if (troopUnit.CurrentAction == UnitAction.IDLE)
                {
                    if (enemySoldiers.Count > 0)
                    {
                        // Get a dictionary of enemy soldiers, organized from closest to furthest
                        Dictionary<Unit, float> closestUnits = FindClosestUnits(enemySoldiers);
                        Unit unitToAttack = null;

                        // Checks if the enemy has more soldiers than we want to attack at once
                        if (enemySoldiers.Count > MAX_NBR_OF_UNIT_TO_ATTACK)
                        {
                            // Choose one of the closest units to randomly attack
                            int rand = UnityEngine.Random.Range(0, MAX_NBR_OF_UNIT_TO_ATTACK + 1);

                            // Current index of dictionary; used to determine if we've found the randomly selected unit
                            int currIndex = 0;

                            // Iterate through dictionary & find the unit to attack that was randomly chosen
                            foreach (KeyValuePair<Unit, float> kvp in closestUnits)
                            {
                                // Check if current unit is the randomly selected one
                                if (currIndex == rand)
                                {
                                    // Sets the unit to attack to this unit
                                    unitToAttack = kvp.Key;
                                    break;
                                }
                                currIndex++;
                            }

                            // Attack enemy unit
                            Attack(troopUnit, unitToAttack);
                        }
                        else
                            // Randomly attack one of the enemy soldiers
                            Attack(troopUnit, GameManager.Instance.GetUnit(enemySoldiers[UnityEngine.Random.Range(0, enemySoldiers.Count)]));
                    }
                    else if (enemyArchers.Count > 0)
                    {
                        // Get a dictionary of enemy archers, organized from closest to furthest
                        Dictionary<Unit, float> closestUnits = FindClosestUnits(enemyArchers);
                        Unit unitToAttack = null;

                        // Checks if the enemy has more Archers than we want to attack at once
                        if (enemyArchers.Count > MAX_NBR_OF_UNIT_TO_ATTACK)
                        {
                            // Choose one of the closest units to randomly attack
                            int rand = UnityEngine.Random.Range(0, MAX_NBR_OF_UNIT_TO_ATTACK + 1);

                            // Current index of dictionary; used to determine if we've found the randomly selected unit
                            int currIndex = 0;

                            // Iterate through dictionary & find the unit to attack that was randomly chosen
                            foreach (KeyValuePair<Unit, float> kvp in closestUnits)
                            {
                                // Check if current unit is the randomly selected one
                                if (currIndex == rand)
                                {
                                    // Sets the unit to attack to this unit
                                    unitToAttack = kvp.Key;
                                    break;
                                }
                                currIndex++;
                            }

                            // Attack enemy unit
                            Attack(troopUnit, unitToAttack);
                        }
                        else
                            // Randomly attack one of the enemy archers
                            Attack(troopUnit, GameManager.Instance.GetUnit(enemyArchers[UnityEngine.Random.Range(0, enemyArchers.Count)]));
                    }
                    // If there are barracks to attack
                    else if (enemyBarracks.Count > 0)
                    {
                        Attack(troopUnit, GameManager.Instance.GetUnit(enemyBarracks[UnityEngine.Random.Range(0, enemyBarracks.Count)]));
                    }
                    // If there are bases to attack
                    else if (enemyBases.Count > 0)
                    {
                        Attack(troopUnit, GameManager.Instance.GetUnit(enemyBases[UnityEngine.Random.Range(0, enemyBases.Count)]));
                    }
                    else if (enemyWorkers.Count > 0)
                    {
                        // Get a dictionary of enemy workers, organized from closest to furthest
                        Dictionary<Unit, float> closestUnits = FindClosestUnits(enemyWorkers);
                        Unit unitToAttack = null;

                        // Checks if the enemy has more workers than we want to attack at once
                        if (enemyWorkers.Count > MAX_NBR_OF_UNIT_TO_ATTACK)
                        {
                            // Choose one of the closest units to randomly attack
                            int rand = UnityEngine.Random.Range(0, MAX_NBR_OF_UNIT_TO_ATTACK + 1);

                            // Current index of dictionary; used to determine if we've found the randomly selected unit
                            int currIndex = 0;

                            // Iterate through dictionary & find the unit to attack that was randomly chosen
                            foreach (KeyValuePair<Unit, float> kvp in closestUnits)
                            {
                                // Check if current unit is the randomly selected one
                                if (currIndex == rand)
                                {
                                    // Sets the unit to attack to this unit
                                    unitToAttack = kvp.Key;
                                    break;
                                }
                                currIndex++;
                            }

                            // Attack enemy unit
                            Attack(troopUnit, unitToAttack);
                        }
                        else
                            // Randomly attack one of the enemy workers
                            Attack(troopUnit, GameManager.Instance.GetUnit(enemyWorkers[UnityEngine.Random.Range(0, enemyWorkers.Count)]));
                    }
                    // If there are refineries to attack
                    else if (enemyRefineries.Count > 0)
                    {
                        Attack(troopUnit, GameManager.Instance.GetUnit(enemyRefineries[UnityEngine.Random.Range(0, enemyRefineries.Count)]));
                    }
                }
            }
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Called at the end of each round before remaining units are
        /// destroyed to allow the agent to observe the "win/loss" state
        /// </summary>
        public override void Learn()
        {
            Debug.Log("Nbr Wins: " + AgentNbrWins);

            //Debug.Log("PlanningAgent::Learn");
            Log("value 1");
            Log("value 2");
            Log("value 3a, 3b");
            Log("value 4");
        }

        /// <summary>
        /// Called before each match between two agents.  Matches have
        /// multiple rounds. 
        /// </summary>
        public override void InitializeMatch()
        {
            Debug.Log("Moron's: " + AgentName);
            //Debug.Log("PlanningAgent::InitializeMatch");
        }

        /// <summary>
        /// Called at the beginning of each round in a match.
        /// There are multiple rounds in a single match between two agents.
        /// </summary>
        public override void InitializeRound()
        {
            //Debug.Log("PlanningAgent::InitializeRound");
            buildPositions = new List<Vector3Int>();

            FindProspectiveBuildPositions(UnitType.BASE);

            // Set the main mine and base to "non-existent"
            mainMineNbr = -1;
            mainBaseNbr = -1;

            // Initialize all of the unit lists
            mines = new List<int>();

            myWorkers = new List<int>();
            mySoldiers = new List<int>();
            myArchers = new List<int>();
            myBases = new List<int>();
            myBarracks = new List<int>();
            myRefineries = new List<int>();

            enemyWorkers = new List<int>();
            enemySoldiers = new List<int>();
            enemyArchers = new List<int>();
            enemyBases = new List<int>();
            enemyBarracks = new List<int>();
            enemyRefineries = new List<int>();

            currState = GameState.BaseBuilding;
        }

        /// <summary>
        /// Updates the game state for the Agent - called once per frame for GameManager
        /// Pulls all of the agents from the game and identifies who they belong to
        /// </summary>
        public void UpdateGameState()
        {
            // Update the common resources
            mines = GameManager.Instance.GetUnitNbrsOfType(UnitType.MINE);

            // Update all of my unitNbrs
            myWorkers = GameManager.Instance.GetUnitNbrsOfType(UnitType.WORKER, AgentNbr);
            mySoldiers = GameManager.Instance.GetUnitNbrsOfType(UnitType.SOLDIER, AgentNbr);
            myArchers = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, AgentNbr);
            myBarracks = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, AgentNbr);
            myBases = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, AgentNbr);
            myRefineries = GameManager.Instance.GetUnitNbrsOfType(UnitType.REFINERY, AgentNbr);

            // Update the enemy agents & unitNbrs
            List<int> enemyAgentNbrs = GameManager.Instance.GetEnemyAgentNbrs(AgentNbr);
            if (enemyAgentNbrs.Any())
            {
                enemyAgentNbr = enemyAgentNbrs[0];
                enemyWorkers = GameManager.Instance.GetUnitNbrsOfType(UnitType.WORKER, enemyAgentNbr);
                enemySoldiers = GameManager.Instance.GetUnitNbrsOfType(UnitType.SOLDIER, enemyAgentNbr);
                enemyArchers = GameManager.Instance.GetUnitNbrsOfType(UnitType.ARCHER, enemyAgentNbr);
                enemyBarracks = GameManager.Instance.GetUnitNbrsOfType(UnitType.BARRACKS, enemyAgentNbr);
                enemyBases = GameManager.Instance.GetUnitNbrsOfType(UnitType.BASE, enemyAgentNbr);
                enemyRefineries = GameManager.Instance.GetUnitNbrsOfType(UnitType.REFINERY, enemyAgentNbr);
                Debug.Log("<color=red>Enemy gold</color>: " + GameManager.Instance.GetAgent(enemyAgentNbr).Gold);
            }
        }

        /// <summary>
        /// Update the GameManager - called once per frame
        /// </summary>
        public override void Update()
        {
            Debug.Log("Update");
            UpdateGameState();

            switch (currState)
            {
                case GameState.BaseBuilding:

                    // Assign main mine
                    if (mines.Count > 0)
                    {
                        if (mainMineNbr == -1)
                        {
                            // Set mainMineNbr to first non-empty mine
                            for (int i = 0; i < mines.Count; i++)
                            {
                                if (GameManager.Instance.GetUnit(mines[i]).Health > 0)
                                    mainMineNbr = mines[i];
                            }
                            // Save position of starting worker
                            Vector3Int workerPos = Vector3Int.RoundToInt(GameManager.Instance.GetUnit(myWorkers[0]).transform.position);
                            Unit mainMine = GameManager.Instance.GetUnit(mainMineNbr);

                            // iterate through all mines & find the closest non-empty one to the starting unit
                            for (int i = 0; i < mines.Count; i++)
                            {
                                Unit checkedMine = GameManager.Instance.GetUnit(mines[i]);

                                // Checks that the mine isn't empty
                                if (checkedMine.Health > 0)
                                {
                                    // Save mine positions
                                    Vector3Int mainMinePos = Vector3Int.RoundToInt(mainMine.transform.position);
                                    Vector3Int minePos = Vector3Int.RoundToInt(GameManager.Instance.GetUnit(mines[i]).transform.position);

                                    // Save path distances between starter unit and mines
                                    float mainMineDist = GameManager.Instance.GetPathToUnit(workerPos, UnitType.MINE, mainMinePos).Count;
                                    float mineDist = GameManager.Instance.GetPathToUnit(workerPos, UnitType.MINE, minePos).Count;

                                    // Determines if currently checked mine is closer than the current mine
                                    if (mineDist < mainMineDist)
                                        mainMineNbr = mines[i]; // Set the current mine to the current mine
                                }
                            }
                        }
                        // If main mine doesn't exist (became empty), choose a new mine to use
                        else if (GameManager.Instance.GetUnit(mainMineNbr) == null)
                        {
                            Debug.LogWarning("Main mine doesn't exist");
                            if (GameManager.Instance.GetUnit(secondMineNbr) != null)
                            {
                                Debug.LogWarning("Second mine exists; replacing second mine with main mine");
                                mainMineNbr = secondMineNbr;
                                secondMineNbr = -1;
                            }
                            else
                                mainMineNbr = -1;
                        }

                    }
                    else
                    {
                        mainMineNbr = -1;
                        secondMineNbr = -1;
                    }

                    if (myBases.Count == 0 && Gold >= Constants.COST[UnitType.BASE] && mainMineNbr != -1)
                    {
                        unitTypeToBuildAround = UnitType.MINE;
                        unitToBuildAroundPos = Vector3Int.RoundToInt(GameManager.Instance.GetUnit(mainMineNbr).transform.position);
                        BuildBuilding(UnitType.BASE);
                    }

                    // If we have at least one base
                    if (myBases.Count > 0)
                    {
                        // Assume the first one is our "main" base
                        if (mainBaseNbr == -1)
                        {
                            if (secondaryBaseNbr != -1)
                                secondaryBaseNbr = -1;

                            mainBaseNbr = myBases[0];
                        }

                        // If we don't have at least half of our workers, train more workers
                        if (myWorkers.Count < MAX_NBR_WORKERS && Gold >= Constants.COST[UnitType.WORKER])
                        {
                            currState = GameState.ArmyBuilding;
                            break;
                        }

                        // If we don't have any barracks, or we have max refineries built, build a barracks
                        else if (myBarracks.Count == 0 && Gold >= Constants.COST[UnitType.BARRACKS] ||
                            myRefineries.Count >= MAX_NBR_REFINERIES && Gold >= Constants.COST[UnitType.BARRACKS])
                        {
                            unitTypeToBuildAround = UnitType.BASE;

                            if (enemyBases.Count > 0)
                                unitToBuildAroundPos = Vector3Int.RoundToInt(GameManager.Instance.GetUnit(enemyBases[0]).transform.position);
                            else
                                unitToBuildAroundPos = Vector3Int.RoundToInt(GameManager.Instance.GetUnit(mainBaseNbr).transform.position);

                            BuildBuilding(UnitType.BARRACKS);
                        }

                        // If I have a barracks built
                        else if (myBarracks.Count > 0)
                        {
                            // If we only have one base built, build another base near a new mine
                            if (myBases.Count == 1 && Gold >= Constants.COST[UnitType.BASE])
                            {
                                for (int i = 0; i < mines.Count; i++)
                                {
                                    if (mines[i] != mainMineNbr)
                                        secondMineNbr = mines[i];
                                }

                                unitTypeToBuildAround = UnitType.MINE;
                                unitToBuildAroundPos = Vector3Int.RoundToInt(GameManager.Instance.GetUnit(secondMineNbr).transform.position);
                                BuildBuilding(UnitType.BASE);
                            }

                            // If I have 2 bases built, build a refinery
                            else if (myBases.Count == 2)
                            {
                                if (secondaryBaseNbr == -1)
                                    secondaryBaseNbr = myBases[1];

                                // If we don't have max refineries built, build a refinery
                                if (myRefineries.Count < MAX_NBR_REFINERIES && Gold >= Constants.COST[UnitType.REFINERY])
                                {
                                    BuildBuilding(UnitType.REFINERY);
                                }
                            }
                        }
                    }

                    Mine();

                    // Checks if we have prereq to switch to training units
                    if (myBases.Count > 0 && myBarracks.Count > 0)
                        currState = GameState.ArmyBuilding;
                    break;

                case GameState.ArmyBuilding:

                    // Set all workers to mine
                    Mine();

                    // If I have less than max workers, train more workers at the main base
                    if (myWorkers.Count < MAX_NBR_WORKERS)
                    {
                        // Get the base unit
                        Unit baseUnit = GameManager.Instance.GetUnit(mainBaseNbr);

                        // If the base exists, is idle, we need a worker, and we have gold
                        if (baseUnit != null && baseUnit.IsBuilt
                                             && baseUnit.CurrentAction == UnitAction.IDLE
                                             && Gold >= Constants.COST[UnitType.WORKER])
                        {
                            Train(baseUnit, UnitType.WORKER);
                        }
                    }

                    // If we don't have a barracks, go back to base building 
                    if (myBarracks.Count == 0)
                    {
                        currState = GameState.BaseBuilding;
                        break;
                    }

                    // For each barracks, determine if it should train a soldier or an archer
                    foreach (int barracksNbr in myBarracks)
                    {
                        // Get the barracks
                        Unit barracksUnit = GameManager.Instance.GetUnit(barracksNbr);

                        // If this barracks still exists, is idle, we need archers, and have gold
                        if (barracksUnit != null && barracksUnit.IsBuilt
                                 && barracksUnit.CurrentAction == UnitAction.IDLE
                                 && Gold >= Constants.COST[UnitType.ARCHER]
                                 && myArchers.Count < MAX_NBR_ARCHERS
                                 && mySoldiers.Count == 0)
                        {
                            Train(barracksUnit, UnitType.ARCHER);
                        }

                        // If this barracks still exists, is idle, we need soldiers, and have gold
                        if (barracksUnit != null && barracksUnit.IsBuilt
                            && barracksUnit.CurrentAction == UnitAction.IDLE
                            && Gold >= Constants.COST[UnitType.SOLDIER])
                        {
                            Train(barracksUnit, UnitType.SOLDIER);
                        }



                    }

                    // If I have max workers, train more workers at the second base (So I have double max workers in total)
                    if (myWorkers.Count >= MAX_NBR_WORKERS && myWorkers.Count < MAX_NBR_WORKERS * 2)
                    {
                        // Get the base unit
                        Unit baseUnit = GameManager.Instance.GetUnit(secondaryBaseNbr);

                        // If the base exists, is idle, we need a worker, and we have gold
                        if (baseUnit != null && baseUnit.IsBuilt
                                             && baseUnit.CurrentAction == UnitAction.IDLE
                                             && Gold >= Constants.COST[UnitType.WORKER])
                        {
                            Train(baseUnit, UnitType.WORKER);
                        }
                    }

                    // When army is of certain size, begin attacking 
                    if (mySoldiers.Count + myArchers.Count > 0)
                        currState = GameState.Attacking;
                    else
                        currState = GameState.BaseBuilding;

                    break;

                case GameState.Attacking:

                    Mine();

                    // For any troops, attack the enemy
                    AttackEnemy(mySoldiers);
                    AttackEnemy(myArchers);

                    // Go back to building more barracks/ troops
                    currState = GameState.BaseBuilding;
                    break;
            }
        }

        // Sends all workers to go mine
        void Mine()
        {
            if (mines.Count > 0)
            {
                // For each worker
                foreach (int worker in myWorkers)
                {
                    // Grab the unit we need for this function
                    Unit unit = GameManager.Instance.GetUnit(worker);

                    Vector3Int unitPos = Vector3Int.RoundToInt(unit.transform.position);

                    // Make sure this unit actually exists and is idle
                    if (unit != null && unit.CurrentAction == UnitAction.IDLE && mainBaseNbr >= 0 && mines.Count >= 0)
                    {
                        Unit mineUnit = GameManager.Instance.GetUnit(mainMineNbr);
                        float mainMineDist = GameManager.Instance.GetPathToUnit(unitPos, UnitType.MINE,
                            Vector3Int.RoundToInt(mineUnit.transform.position)).Count;

                        // Grab the closest mine
                        for (int i = 0; i < mines.Count; i++)
                        {
                            // Save mine positions
                            Vector3Int minePos = Vector3Int.RoundToInt(GameManager.Instance.GetUnit(mines[i]).transform.position);

                            // Save path distances between current worker and mine
                            float mineDist = GameManager.Instance.GetPathToUnit(unitPos, UnitType.MINE, minePos).Count;

                            // Determines if checked mine is closer than the current closest mine
                            if (mineDist < mainMineDist)
                                mineUnit = GameManager.Instance.GetUnit(mines[i]);
                        }

                        Unit baseUnit = GameManager.Instance.GetUnit(mainBaseNbr);
                        float mainBaseDist = GameManager.Instance.GetPathToUnit(unitPos, UnitType.BASE,
                            Vector3Int.RoundToInt(baseUnit.transform.position)).Count;

                        // Grab the closest base
                        for (int i = 0; i < myBases.Count; i++)
                        {
                            // Save base position
                            Vector3Int basePos = Vector3Int.RoundToInt(GameManager.Instance.GetUnit(myBases[i]).transform.position);

                            // Save path distances between current worker and base
                            float baseDist = GameManager.Instance.GetPathToUnit(unitPos, UnitType.BASE, basePos).Count;

                            // Determines if checked base is closer than the current closest base
                            if (baseDist < mainBaseDist)
                                baseUnit = GameManager.Instance.GetUnit(myBases[i]);
                        }

                        if (mineUnit != null && baseUnit != null && mineUnit.Health > 0)
                        {
                            Gather(unit, mineUnit, baseUnit);
                        }
                    }
                }
            }
        }

        Dictionary<Unit, float> FindClosestUnits(List<int> enemyType)
        {
            Unit mainBase = GameManager.Instance.GetUnit(mainBaseNbr);
            Dictionary<Unit, float> unitDistDict = new Dictionary<Unit, float>();

            if (enemyType.Count > 0)
            {
                for (int i = 0; i < enemyType.Count; i++)
                {
                    Unit enemy = GameManager.Instance.GetUnit(enemyType[i]);
                    float distToBase = Vector3Int.Distance(Vector3Int.RoundToInt(mainBase.transform.position),
                        Vector3Int.RoundToInt(enemy.transform.position));
                    unitDistDict.Add(enemy, distToBase);
                }
            }

            // Sort list based on unit's distance to base
            unitDistDict = unitDistDict.OrderBy(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return unitDistDict;
        }
        #endregion
    }
}