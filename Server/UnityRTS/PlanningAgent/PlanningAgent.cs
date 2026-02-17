using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace PlanningAgent
{
    ///<summary>Planning Agent is the over-head planner that decided where
    /// individual units go and what tasks they perform.  Low-level
    /// AI is handled by other classes (like pathfinding).
    ///</summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_NBR_WORKERS = 10;
        private const int MAX_NBR_BASES = 2;
        private const int MAX_NBR_BARRACKS = 5;
        private const int MAX_NBR_REFINERIES = 2;
        private const int MAX_NBR_SOLDIERS = 40;
        private const int MAX_NBR_ARCHERS = 10;

        private const int MAX_NBR_OF_UNIT_TO_ATTACK = 5;

        #region Private Data

        private int secondMineNbr = -1;
        private int secondaryBaseNbr = -1;

        private Random rng = new Random();

        private enum GameState
        {
            BaseBuilding,
            ArmyBuilding,
            Attacking
        }

        private GameState currState = GameState.BaseBuilding;

        private UnitType unitTypeToBuildAround;
        private Position unitToBuildAroundPos = Position.Zero;

        private Position buildPos = Position.Zero;

        /// <summary>
        /// Build a building
        /// </summary>
        public void BuildBuilding(UnitType unitType, IGameState state, IAgentActions actions)
        {
            // For each worker
            foreach (int worker in myWorkers)
            {
                UnitInfo? unitInfo = state.GetUnit(worker);

                // Make sure this unit actually exists & they're only gathering gold
                if (unitInfo.HasValue && (unitInfo.Value.CurrentAction == UnitAction.GATHER || unitInfo.Value.CurrentAction == UnitAction.IDLE))
                {
                    Position unitPos = unitInfo.Value.GridPosition;
                    float dist = Position.Distance(unitPos, buildPos);

                    // IF we're trying to build a base, find the closest possible space to it
                    if (unitType == UnitType.BASE)
                    {
                        // Save list of all buildable grid positions near where we want to build
                        IReadOnlyList<Position> buildablePositions = state.GetBuildablePositionsNearUnit(
                            unitTypeToBuildAround, unitToBuildAroundPos);

                        if (buildablePositions.Count > 0)
                        {
                            buildPos = buildablePositions[0];

                            // Find the closest buildable grid position to the current unit
                            for (int i = 0; i < buildablePositions.Count; i++)
                            {
                                Position checkingBuildPos = buildablePositions[i];
                                float checkingDist = Position.Distance(unitPos, checkingBuildPos);

                                if (checkingDist < dist)
                                {
                                    // Check if the buildable grid position is a buildable area
                                    if (state.IsBoundedAreaBuildable(unitType, checkingBuildPos))
                                    {
                                        dist = checkingDist;
                                        buildPos = buildablePositions[i];
                                    }
                                }
                            }
                        }
                    }

                    // If the area chosen above isn't buildable OR we're not building a base, choose a new build location near where we want to build
                    if (!state.IsBoundedAreaBuildable(unitType, buildPos) || unitType != UnitType.BASE)
                    {
                        for (int i = 0; i < buildPositions.Count; i++)
                        {
                            // Check if the current build position is closer to the unit
                            if (Position.Distance(unitToBuildAroundPos, buildPositions[i]) < dist)
                            {
                                // Check if the build position is a buildable area
                                if (state.IsBoundedAreaBuildable(unitType, buildPositions[i]))
                                {
                                    dist = Position.Distance(unitToBuildAroundPos, buildPositions[i]);
                                    buildPos = buildPositions[i];
                                }
                            }
                        }
                    }

                    if (state.IsBoundedAreaBuildable(unitType, buildPos))
                    {
                        actions.Build(worker, buildPos, unitType);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Attack the enemy
        /// </summary>
        public void AttackEnemy(List<int> myTroops, IGameState state, IAgentActions actions)
        {
            // For each of my troops in this collection
            foreach (int troopNbr in myTroops)
            {
                // If this troop is idle, give him something to attack
                UnitInfo? troopInfo = state.GetUnit(troopNbr);
                if (!troopInfo.HasValue || troopInfo.Value.CurrentAction != UnitAction.IDLE)
                    continue;

                if (enemySoldiers.Count > 0)
                {
                    // Get closest enemy soldiers
                    var closestUnits = FindClosestUnits(enemySoldiers, state);

                    if (enemySoldiers.Count > MAX_NBR_OF_UNIT_TO_ATTACK)
                    {
                        int rand = rng.Next(0, MAX_NBR_OF_UNIT_TO_ATTACK + 1);
                        int currIndex = 0;
                        foreach (var kvp in closestUnits)
                        {
                            if (currIndex == rand)
                            {
                                actions.Attack(troopNbr, kvp.Key);
                                break;
                            }
                            currIndex++;
                        }
                    }
                    else
                        actions.Attack(troopNbr, enemySoldiers[rng.Next(0, enemySoldiers.Count)]);
                }
                else if (enemyArchers.Count > 0)
                {
                    var closestUnits = FindClosestUnits(enemyArchers, state);

                    if (enemyArchers.Count > MAX_NBR_OF_UNIT_TO_ATTACK)
                    {
                        int rand = rng.Next(0, MAX_NBR_OF_UNIT_TO_ATTACK + 1);
                        int currIndex = 0;
                        foreach (var kvp in closestUnits)
                        {
                            if (currIndex == rand)
                            {
                                actions.Attack(troopNbr, kvp.Key);
                                break;
                            }
                            currIndex++;
                        }
                    }
                    else
                        actions.Attack(troopNbr, enemyArchers[rng.Next(0, enemyArchers.Count)]);
                }
                else if (enemyBarracks.Count > 0)
                {
                    actions.Attack(troopNbr, enemyBarracks[rng.Next(0, enemyBarracks.Count)]);
                }
                else if (enemyBases.Count > 0)
                {
                    actions.Attack(troopNbr, enemyBases[rng.Next(0, enemyBases.Count)]);
                }
                else if (enemyWorkers.Count > 0)
                {
                    var closestUnits = FindClosestUnits(enemyWorkers, state);

                    if (enemyWorkers.Count > MAX_NBR_OF_UNIT_TO_ATTACK)
                    {
                        int rand = rng.Next(0, MAX_NBR_OF_UNIT_TO_ATTACK + 1);
                        int currIndex = 0;
                        foreach (var kvp in closestUnits)
                        {
                            if (currIndex == rand)
                            {
                                actions.Attack(troopNbr, kvp.Key);
                                break;
                            }
                            currIndex++;
                        }
                    }
                    else
                        actions.Attack(troopNbr, enemyWorkers[rng.Next(0, enemyWorkers.Count)]);
                }
                else if (enemyRefineries.Count > 0)
                {
                    actions.Attack(troopNbr, enemyRefineries[rng.Next(0, enemyRefineries.Count)]);
                }
            }
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Called before each match between two agents.  Matches have
        /// multiple rounds.
        /// </summary>
        public override void InitializeMatch()
        {
        }

        /// <summary>
        /// Called at the beginning of each round in a match.
        /// </summary>
        public override void InitializeRound(IGameState state)
        {
            base.InitializeRound(state);
            currState = GameState.BaseBuilding;
        }

        /// <summary>
        /// Update the GameManager - called once per frame
        /// </summary>
        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);

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
                                UnitInfo? mineInfo = state.GetUnit(mines[i]);
                                if (mineInfo.HasValue && mineInfo.Value.Health > 0)
                                    mainMineNbr = mines[i];
                            }

                            // Can't proceed without workers or a valid mine
                            if (myWorkers.Count == 0 || mainMineNbr == -1)
                                break;

                            // Save position of starting worker
                            UnitInfo? workerInfo = state.GetUnit(myWorkers[0]);
                            if (!workerInfo.HasValue) break;
                            Position workerPos = workerInfo.Value.GridPosition;

                            UnitInfo? mainMineInfo = state.GetUnit(mainMineNbr);
                            if (!mainMineInfo.HasValue) break;

                            // iterate through all mines & find the closest non-empty one to the starting unit
                            for (int i = 0; i < mines.Count; i++)
                            {
                                UnitInfo? checkedMineInfo = state.GetUnit(mines[i]);

                                // Checks that the mine isn't empty
                                if (checkedMineInfo.HasValue && checkedMineInfo.Value.Health > 0)
                                {
                                    // Save mine positions
                                    mainMineInfo = state.GetUnit(mainMineNbr);
                                    Position mainMinePos = mainMineInfo.Value.GridPosition;
                                    Position minePos = checkedMineInfo.Value.GridPosition;

                                    // Save path distances between starter unit and mines
                                    float mainMineDist = state.GetPathToUnit(workerPos, UnitType.MINE, mainMinePos).Count;
                                    float mineDist = state.GetPathToUnit(workerPos, UnitType.MINE, minePos).Count;

                                    // Determines if currently checked mine is closer than the current mine
                                    if (mineDist < mainMineDist)
                                        mainMineNbr = mines[i];
                                }
                            }
                        }
                        // If main mine doesn't exist (became empty), choose a new mine to use
                        else if (!state.GetUnit(mainMineNbr).HasValue)
                        {
                            if (state.GetUnit(secondMineNbr).HasValue)
                            {
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

                    if (myBases.Count == 0 && state.MyGold >= GameConstants.COST[UnitType.BASE] && mainMineNbr != -1)
                    {
                        unitTypeToBuildAround = UnitType.MINE;
                        UnitInfo? mineInfo = state.GetUnit(mainMineNbr);
                        if (mineInfo.HasValue)
                        {
                            unitToBuildAroundPos = mineInfo.Value.GridPosition;
                            BuildBuilding(UnitType.BASE, state, actions);
                        }
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
                        if (myWorkers.Count < MAX_NBR_WORKERS && state.MyGold >= GameConstants.COST[UnitType.WORKER])
                        {
                            currState = GameState.ArmyBuilding;
                            break;
                        }

                        // If we don't have any barracks, or we have max refineries built, build a barracks (requires built base)
                        else if (HasBuiltUnit(myBases, state) &&
                            (myBarracks.Count == 0 && state.MyGold >= GameConstants.COST[UnitType.BARRACKS] ||
                            myRefineries.Count >= MAX_NBR_REFINERIES && state.MyGold >= GameConstants.COST[UnitType.BARRACKS]))
                        {
                            unitTypeToBuildAround = UnitType.BASE;

                            if (enemyBases.Count > 0)
                            {
                                UnitInfo? enemyBaseInfo = state.GetUnit(enemyBases[0]);
                                if (enemyBaseInfo.HasValue)
                                    unitToBuildAroundPos = enemyBaseInfo.Value.GridPosition;
                            }
                            else
                            {
                                UnitInfo? mainBaseInfo = state.GetUnit(mainBaseNbr);
                                if (mainBaseInfo.HasValue)
                                    unitToBuildAroundPos = mainBaseInfo.Value.GridPosition;
                            }

                            BuildBuilding(UnitType.BARRACKS, state, actions);
                        }

                        // If I have a barracks built
                        else if (myBarracks.Count > 0)
                        {
                            // If we only have one base built, build another base near a new mine
                            if (myBases.Count == 1 && state.MyGold >= GameConstants.COST[UnitType.BASE])
                            {
                                for (int i = 0; i < mines.Count; i++)
                                {
                                    if (mines[i] != mainMineNbr)
                                        secondMineNbr = mines[i];
                                }

                                if (secondMineNbr != -1)
                                {
                                    UnitInfo? secondMineInfo = state.GetUnit(secondMineNbr);
                                    if (secondMineInfo.HasValue)
                                    {
                                        unitTypeToBuildAround = UnitType.MINE;
                                        unitToBuildAroundPos = secondMineInfo.Value.GridPosition;
                                        BuildBuilding(UnitType.BASE, state, actions);
                                    }
                                }
                            }

                            // If I have 2 bases built, build a refinery (requires built base + barracks)
                            else if (myBases.Count == 2)
                            {
                                if (secondaryBaseNbr == -1)
                                    secondaryBaseNbr = myBases[1];

                                // If we don't have max refineries built, build a refinery
                                if (HasBuiltUnit(myBases, state) && HasBuiltUnit(myBarracks, state) &&
                                    myRefineries.Count < MAX_NBR_REFINERIES && state.MyGold >= GameConstants.COST[UnitType.REFINERY])
                                {
                                    BuildBuilding(UnitType.REFINERY, state, actions);
                                }
                            }
                        }
                    }

                    Mine(state, actions);

                    // Checks if we have prereq to switch to training units
                    if (myBases.Count > 0 && myBarracks.Count > 0)
                        currState = GameState.ArmyBuilding;
                    break;

                case GameState.ArmyBuilding:

                    // Set all workers to mine
                    Mine(state, actions);

                    // If I have less than max workers, train more workers at the main base
                    if (myWorkers.Count < MAX_NBR_WORKERS)
                    {
                        UnitInfo? baseInfo = state.GetUnit(mainBaseNbr);

                        if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                                             && baseInfo.Value.CurrentAction == UnitAction.IDLE
                                             && state.MyGold >= GameConstants.COST[UnitType.WORKER])
                        {
                            actions.Train(mainBaseNbr, UnitType.WORKER);
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
                        UnitInfo? barracksInfo = state.GetUnit(barracksNbr);

                        // If this barracks still exists, is idle, we need archers, and have gold
                        if (barracksInfo.HasValue && barracksInfo.Value.IsBuilt
                                 && barracksInfo.Value.CurrentAction == UnitAction.IDLE
                                 && state.MyGold >= GameConstants.COST[UnitType.ARCHER]
                                 && myArchers.Count < MAX_NBR_ARCHERS
                                 && mySoldiers.Count == 0)
                        {
                            actions.Train(barracksNbr, UnitType.ARCHER);
                        }

                        // If this barracks still exists, is idle, we need soldiers, and have gold
                        if (barracksInfo.HasValue && barracksInfo.Value.IsBuilt
                            && barracksInfo.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.SOLDIER])
                        {
                            actions.Train(barracksNbr, UnitType.SOLDIER);
                        }
                    }

                    // If I have max workers, train more workers at the second base
                    if (myWorkers.Count >= MAX_NBR_WORKERS && myWorkers.Count < MAX_NBR_WORKERS * 2)
                    {
                        UnitInfo? baseInfo = state.GetUnit(secondaryBaseNbr);

                        if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                                             && baseInfo.Value.CurrentAction == UnitAction.IDLE
                                             && state.MyGold >= GameConstants.COST[UnitType.WORKER])
                        {
                            actions.Train(secondaryBaseNbr, UnitType.WORKER);
                        }
                    }

                    // When army is of certain size, begin attacking
                    if (mySoldiers.Count + myArchers.Count > 0)
                        currState = GameState.Attacking;
                    else
                        currState = GameState.BaseBuilding;

                    break;

                case GameState.Attacking:

                    Mine(state, actions);

                    // For any troops, attack the enemy
                    AttackEnemy(mySoldiers, state, actions);
                    AttackEnemy(myArchers, state, actions);

                    // Go back to building more barracks/ troops
                    currState = GameState.BaseBuilding;
                    break;
            }
        }

        // Sends all workers to go mine
        void Mine(IGameState state, IAgentActions actions)
        {
            if (mines.Count > 0)
            {
                // For each worker
                foreach (int worker in myWorkers)
                {
                    UnitInfo? unitInfo = state.GetUnit(worker);
                    if (!unitInfo.HasValue) continue;

                    Position unitPos = unitInfo.Value.GridPosition;

                    // Make sure this unit actually exists and is idle
                    if (unitInfo.Value.CurrentAction == UnitAction.IDLE && mainBaseNbr >= 0 && mines.Count >= 0)
                    {
                        UnitInfo? mineInfo = state.GetUnit(mainMineNbr);
                        if (!mineInfo.HasValue) continue;

                        int closestMineNbr = mainMineNbr;
                        float mainMineDist = state.GetPathToUnit(unitPos, UnitType.MINE,
                            mineInfo.Value.GridPosition).Count;

                        // Grab the closest mine
                        for (int i = 0; i < mines.Count; i++)
                        {
                            UnitInfo? checkedMineInfo = state.GetUnit(mines[i]);
                            if (!checkedMineInfo.HasValue) continue;

                            Position minePos = checkedMineInfo.Value.GridPosition;
                            float mineDist = state.GetPathToUnit(unitPos, UnitType.MINE, minePos).Count;

                            if (mineDist < mainMineDist)
                            {
                                closestMineNbr = mines[i];
                                mainMineDist = mineDist;
                            }
                        }

                        UnitInfo? baseInfo = state.GetUnit(mainBaseNbr);
                        if (!baseInfo.HasValue) continue;

                        int closestBaseNbr = mainBaseNbr;
                        float mainBaseDist = state.GetPathToUnit(unitPos, UnitType.BASE,
                            baseInfo.Value.GridPosition).Count;

                        // Grab the closest base
                        for (int i = 0; i < myBases.Count; i++)
                        {
                            UnitInfo? checkedBaseInfo = state.GetUnit(myBases[i]);
                            if (!checkedBaseInfo.HasValue) continue;

                            Position basePos = checkedBaseInfo.Value.GridPosition;
                            float baseDist = state.GetPathToUnit(unitPos, UnitType.BASE, basePos).Count;

                            if (baseDist < mainBaseDist)
                            {
                                closestBaseNbr = myBases[i];
                                mainBaseDist = baseDist;
                            }
                        }

                        UnitInfo? closestMineInfo = state.GetUnit(closestMineNbr);
                        if (closestMineInfo.HasValue && closestMineInfo.Value.Health > 0)
                        {
                            actions.Gather(worker, closestMineNbr, closestBaseNbr);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Find closest enemy units sorted by distance to main base
        /// Returns dictionary of unitNbr -> distance
        /// </summary>
        Dictionary<int, float> FindClosestUnits(List<int> enemyType, IGameState state)
        {
            UnitInfo? mainBaseInfo = state.GetUnit(mainBaseNbr);
            Dictionary<int, float> unitDistDict = new Dictionary<int, float>();

            if (!mainBaseInfo.HasValue) return unitDistDict;

            Position basePos = mainBaseInfo.Value.GridPosition;

            if (enemyType.Count > 0)
            {
                for (int i = 0; i < enemyType.Count; i++)
                {
                    UnitInfo? enemyInfo = state.GetUnit(enemyType[i]);
                    if (!enemyInfo.HasValue) continue;

                    float distToBase = Position.Distance(basePos, enemyInfo.Value.GridPosition);
                    unitDistDict[enemyType[i]] = distToBase;
                }
            }

            // Sort by distance
            unitDistDict = unitDistDict.OrderBy(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            return unitDistDict;
        }
        #endregion
    }
}
