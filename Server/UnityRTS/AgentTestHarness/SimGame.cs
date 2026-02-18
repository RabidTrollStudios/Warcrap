using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Tick-based game loop simulator. Orchestrates the full agent lifecycle
    /// without any Unity dependencies.
    ///
    /// Tick order:
    /// 1. Call agent.Update(state, actions) for each agent
    /// 2. Process queued commands (validate, deduct gold, set unit actions)
    /// 3. Advance all in-progress unit tasks (move, train, build, gather, attack)
    /// 4. Remove dead units (health &lt;= 0)
    /// </summary>
    public class SimGame
    {
        public SimMap Map { get; }
        public SimConfig Config { get; }
        public int CurrentTick { get; private set; }

        // Per-agent state
        internal int[] Gold;
        internal int[] Wins;

        // All units keyed by UnitNbr
        internal Dictionary<int, SimUnit> Units = new Dictionary<int, SimUnit>();
        internal int NextUnitNbr;

        // Agent references
        private IPlanningAgent[] agents = new IPlanningAgent[2];
        private SimGameState[] states = new SimGameState[2];
        private SimAgentActions[] actions = new SimAgentActions[2];

        // Derived timing constants (computed from Config.GameSpeed)
        private float scalarCreationTime;
        private Dictionary<UnitType, float> creationTime;
        private Dictionary<UnitType, float> damage;
        private float miningSpeed; // gold per second for WORKER
        private float miningCapacity; // gold per trip

        internal SimGame(SimConfig config, SimMap map)
        {
            Config = config;
            Map = map;
            Gold = new int[] { config.StartingGold, config.StartingGold };
            Wins = new int[] { 0, 0 };
            CurrentTick = 0;

            ComputeDerivedConstants();

            states[0] = new SimGameState(this, 0);
            states[1] = new SimGameState(this, 1);
            actions[0] = new SimAgentActions(this, 0);
            actions[1] = new SimAgentActions(this, 1);
        }

        private void ComputeDerivedConstants()
        {
            int gs = Config.GameSpeed;
            scalarCreationTime = gs > 0 ? 1f / gs : float.PositiveInfinity;

            creationTime = new Dictionary<UnitType, float>
            {
                { UnitType.MINE, 0f },
                { UnitType.WORKER, scalarCreationTime * 2f },
                { UnitType.SOLDIER, scalarCreationTime * 4f },
                { UnitType.ARCHER, scalarCreationTime * 5f },
                { UnitType.BASE, scalarCreationTime * 10f },
                { UnitType.BARRACKS, scalarCreationTime * 15f },
                { UnitType.REFINERY, scalarCreationTime * 15f },
            };

            float scalarDamage = gs;
            damage = new Dictionary<UnitType, float>
            {
                { UnitType.MINE, 0f },
                { UnitType.WORKER, 0f },
                { UnitType.SOLDIER, 20f * scalarDamage },
                { UnitType.ARCHER, 3f * scalarDamage },
                { UnitType.BASE, 0f },
                { UnitType.BARRACKS, 0f },
                { UnitType.REFINERY, 0f },
            };

            float miningBoost = GameConstants.MINING_BOOST;
            miningSpeed = gs * miningBoost * 20f; // gold per second
            miningCapacity = GameConstants.MINING_CAPACITY[UnitType.WORKER];
        }

        #region Agent Setup

        public void SetAgent(int agentNbr, IPlanningAgent agent)
        {
            agents[agentNbr] = agent;
        }

        /// <summary>
        /// Place a unit on the map. Updates grid buildability.
        /// </summary>
        internal SimUnit PlaceUnit(int ownerAgentNbr, UnitType unitType, Position position, float health, bool isBuilt)
        {
            var unit = new SimUnit(NextUnitNbr++, unitType, ownerAgentNbr, position, health, isBuilt);
            Units[unit.UnitNbr] = unit;
            Map.SetAreaBuildability(unitType, position, false);
            return unit;
        }

        #endregion

        #region Game Lifecycle

        /// <summary>
        /// Call InitializeMatch on both agents.
        /// </summary>
        public void InitializeMatch()
        {
            agents[0]?.InitializeMatch();
            agents[1]?.InitializeMatch();
        }

        /// <summary>
        /// Call InitializeRound on both agents.
        /// </summary>
        public void InitializeRound()
        {
            agents[0]?.InitializeRound(states[0]);
            agents[1]?.InitializeRound(states[1]);
        }

        /// <summary>
        /// Call Learn on both agents.
        /// </summary>
        public void Learn()
        {
            agents[0]?.Learn(states[0]);
            agents[1]?.Learn(states[1]);
        }

        /// <summary>
        /// Advance the simulation by one tick.
        /// </summary>
        public void Tick()
        {
            CurrentTick++;

            // 1. Call agent Update
            for (int a = 0; a < 2; a++)
            {
                actions[a].ClearPending();
                agents[a]?.Update(states[a], actions[a]);
            }

            // 2. Process queued commands
            for (int a = 0; a < 2; a++)
                ProcessCommands(actions[a]);

            // 3. Advance in-progress tasks
            AdvanceAllUnits();

            // 4. Remove dead units
            RemoveDeadUnits();
        }

        /// <summary>Run the simulation for a fixed number of ticks.</summary>
        public void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                Tick();
        }

        /// <summary>
        /// Run until a predicate is true or maxTicks is exceeded.
        /// Returns true if predicate was satisfied.
        /// </summary>
        public bool RunUntil(Func<SimGame, bool> predicate, int maxTicks = 10000)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                if (predicate(this)) return true;
                Tick();
            }
            return predicate(this);
        }

        #endregion

        #region Query Helpers

        public int GetGold(int agentNbr) => Gold[agentNbr];
        public int GetWins(int agentNbr) => Wins[agentNbr];

        public SimUnit GetUnit(int unitNbr)
        {
            Units.TryGetValue(unitNbr, out var unit);
            return unit;
        }

        public List<SimUnit> GetUnitsByType(int agentNbr, UnitType type)
        {
            return Units.Values
                .Where(u => u.OwnerAgentNbr == agentNbr && u.UnitType == type)
                .ToList();
        }

        public List<string> GetLogs(int agentNbr)
        {
            return actions[agentNbr].LogMessages;
        }

        #endregion

        #region Command Processing

        private void ProcessCommands(SimAgentActions agentActions)
        {
            foreach (var cmd in agentActions.PendingCommands)
            {
                if (!Units.TryGetValue(cmd.UnitNbr, out var unit))
                    continue;

                switch (cmd.Type)
                {
                    case CommandType.Move:
                        ProcessMove(unit, cmd.Target);
                        break;
                    case CommandType.Build:
                        ProcessBuild(unit, cmd.Target, cmd.UnitType);
                        break;
                    case CommandType.Gather:
                        ProcessGather(unit, cmd.MineNbr, cmd.BaseNbr);
                        break;
                    case CommandType.Train:
                        ProcessTrain(unit, cmd.UnitType);
                        break;
                    case CommandType.Attack:
                        ProcessAttack(unit, cmd.TargetUnitNbr);
                        break;
                }
            }
        }

        private void ProcessMove(SimUnit unit, Position target)
        {
            var path = Map.FindPath(unit.GridPosition, target);
            if (path.Count == 0) return;

            unit.CurrentAction = UnitAction.MOVE;
            unit.Path = path;
            unit.PathIndex = 0;
        }

        private void ProcessBuild(SimUnit worker, Position target, UnitType buildingType)
        {
            // Re-validate gold (may have been spent by earlier command this tick)
            float cost = GameConstants.COST[buildingType];
            if (Gold[worker.OwnerAgentNbr] < cost) return;
            if (!Map.IsAreaBuildable(buildingType, target)) return;

            // Deduct gold at build start
            Gold[worker.OwnerAgentNbr] -= (int)cost;

            // Place the building immediately (unbuilt)
            var building = PlaceUnit(worker.OwnerAgentNbr, buildingType, target,
                GameConstants.HEALTH[buildingType], false);

            // Path worker to a cell adjacent to the building
            var path = Map.FindPathToUnit(worker.GridPosition, buildingType, target);

            worker.CurrentAction = UnitAction.BUILD;
            worker.BuildTarget = buildingType;
            worker.BuildSite = target;
            worker.BuildPlaced = true;
            worker.BuildTimer = creationTime[buildingType];
            worker.Path = path;
            worker.PathIndex = 0;
        }

        private void ProcessGather(SimUnit worker, int mineNbr, int baseNbr)
        {
            if (!Units.TryGetValue(mineNbr, out var mine)) return;
            if (!Units.TryGetValue(baseNbr, out var baseUnit)) return;

            // Path to mine
            var path = Map.FindPathToUnit(worker.GridPosition, UnitType.MINE, mine.GridPosition);
            if (path.Count == 0 && !IsAdjacentToUnit(worker.GridPosition, UnitType.MINE, mine.GridPosition))
                return;

            worker.CurrentAction = UnitAction.GATHER;
            worker.GatherMineNbr = mineNbr;
            worker.GatherBaseNbr = baseNbr;
            worker.GatherPhase = GatherPhase.TO_MINE;
            worker.Path = path;
            worker.PathIndex = 0;
            worker.MiningTimer = 0f;
        }

        private void ProcessTrain(SimUnit building, UnitType unitType)
        {
            // Re-validate (building may have received another command this tick)
            if (building.CurrentAction != UnitAction.IDLE) return;
            float cost = GameConstants.COST[unitType];
            if (Gold[building.OwnerAgentNbr] < cost) return;

            Gold[building.OwnerAgentNbr] -= (int)cost;

            building.CurrentAction = UnitAction.TRAIN;
            building.TrainTarget = unitType;
            building.TrainTimer = creationTime[unitType];
        }

        private void ProcessAttack(SimUnit attacker, int targetNbr)
        {
            if (!Units.ContainsKey(targetNbr)) return;

            // Path toward the target (use FindPathToUnit so we path to an adjacent cell,
            // which handles multi-cell buildings that are unwalkable)
            var target = Units[targetNbr];
            var path = Map.FindPathToUnit(attacker.GridPosition, target.UnitType, target.GridPosition);

            attacker.CurrentAction = UnitAction.ATTACK;
            attacker.AttackTargetNbr = targetNbr;
            attacker.Path = path;
            attacker.PathIndex = 0;
        }

        #endregion

        #region Task Advancement

        private void AdvanceAllUnits()
        {
            // Snapshot unit keys so we can modify the collection
            var unitKeys = Units.Keys.ToList();
            foreach (int key in unitKeys)
            {
                if (!Units.TryGetValue(key, out var unit)) continue;
                switch (unit.CurrentAction)
                {
                    case UnitAction.MOVE:
                        AdvanceMove(unit);
                        break;
                    case UnitAction.TRAIN:
                        AdvanceTrain(unit);
                        break;
                    case UnitAction.BUILD:
                        AdvanceBuild(unit);
                        break;
                    case UnitAction.GATHER:
                        AdvanceGather(unit);
                        break;
                    case UnitAction.ATTACK:
                        AdvanceAttack(unit);
                        break;
                }
            }
        }

        private void AdvanceMove(SimUnit unit)
        {
            if (unit.Path == null || unit.PathIndex >= unit.Path.Count)
            {
                unit.CurrentAction = UnitAction.IDLE;
                unit.Path = null;
                return;
            }

            MoveUnitOneStep(unit);
        }

        private void MoveUnitOneStep(SimUnit unit)
        {
            if (unit.Path == null || unit.PathIndex >= unit.Path.Count) return;

            Position nextPos = unit.Path[unit.PathIndex];

            // Free old cell, occupy new cell
            if (GameConstants.CAN_MOVE[unit.UnitType])
            {
                Map.SetAreaBuildability(unit.UnitType, unit.GridPosition, true);
            }

            unit.GridPosition = nextPos;
            unit.PathIndex++;

            if (GameConstants.CAN_MOVE[unit.UnitType])
            {
                Map.SetAreaBuildability(unit.UnitType, unit.GridPosition, false);
            }

            if (unit.PathIndex >= unit.Path.Count)
            {
                // Path complete — for pure MOVE, go IDLE
                if (unit.CurrentAction == UnitAction.MOVE)
                {
                    unit.CurrentAction = UnitAction.IDLE;
                    unit.Path = null;
                }
            }
        }

        private void AdvanceTrain(SimUnit building)
        {
            building.TrainTimer -= Config.TickDuration;

            if (building.TrainTimer <= 0f)
            {
                // Find a buildable cell adjacent to the building to spawn the unit
                var spawnPositions = Map.GetBuildablePositionsNearUnit(building.UnitType, building.GridPosition);
                if (spawnPositions.Count == 0)
                {
                    // No room — stay in TRAIN state, retry next tick
                    building.TrainTimer = 0.001f;
                    return;
                }

                Position spawnPos = spawnPositions[0];
                float health = GameConstants.HEALTH[building.TrainTarget];
                PlaceUnit(building.OwnerAgentNbr, building.TrainTarget, spawnPos, health, true);

                building.CurrentAction = UnitAction.IDLE;
            }
        }

        private void AdvanceBuild(SimUnit worker)
        {
            // Phase 1: walk to build site
            if (worker.Path != null && worker.PathIndex < worker.Path.Count)
            {
                MoveUnitOneStep(worker);
                // Keep action as BUILD even after step
                worker.CurrentAction = UnitAction.BUILD;
                return;
            }

            // Phase 2: count down build timer
            worker.BuildTimer -= Config.TickDuration;

            if (worker.BuildTimer <= 0f)
            {
                // Mark building as built
                foreach (var u in Units.Values)
                {
                    if (u.UnitType == worker.BuildTarget
                        && u.GridPosition == worker.BuildSite
                        && u.OwnerAgentNbr == worker.OwnerAgentNbr
                        && !u.IsBuilt)
                    {
                        u.IsBuilt = true;
                        break;
                    }
                }

                worker.CurrentAction = UnitAction.IDLE;
                worker.Path = null;
            }
        }

        private void AdvanceGather(SimUnit worker)
        {
            switch (worker.GatherPhase)
            {
                case GatherPhase.TO_MINE:
                    AdvanceGatherToMine(worker);
                    break;
                case GatherPhase.MINING:
                    AdvanceGatherMining(worker);
                    break;
                case GatherPhase.TO_BASE:
                    AdvanceGatherToBase(worker);
                    break;
            }
        }

        private void AdvanceGatherToMine(SimUnit worker)
        {
            // If mine was destroyed, go idle
            if (!Units.TryGetValue(worker.GatherMineNbr, out var mine))
            {
                worker.CurrentAction = UnitAction.IDLE;
                worker.Path = null;
                return;
            }

            // Walk toward the mine
            if (worker.Path != null && worker.PathIndex < worker.Path.Count)
            {
                MoveUnitOneStep(worker);
                worker.CurrentAction = UnitAction.GATHER;
                return;
            }

            // Arrived adjacent to mine — start mining
            worker.GatherPhase = GatherPhase.MINING;
            float miningTime = miningSpeed > 0 ? miningCapacity / miningSpeed : 1f;
            worker.MiningTimer = miningTime;
        }

        private void AdvanceGatherMining(SimUnit worker)
        {
            if (!Units.TryGetValue(worker.GatherMineNbr, out var mine))
            {
                worker.CurrentAction = UnitAction.IDLE;
                return;
            }

            worker.MiningTimer -= Config.TickDuration;

            if (worker.MiningTimer <= 0f)
            {
                // Deduct gold from mine
                float goldMined = Math.Min(miningCapacity, mine.Health);
                mine.Health -= goldMined;

                // Path to base
                if (!Units.TryGetValue(worker.GatherBaseNbr, out var baseUnit))
                {
                    worker.CurrentAction = UnitAction.IDLE;
                    return;
                }

                var path = Map.FindPathToUnit(worker.GridPosition, UnitType.BASE, baseUnit.GridPosition);
                worker.GatherPhase = GatherPhase.TO_BASE;
                worker.Path = path;
                worker.PathIndex = 0;

                // Store gold carried as a small temporary value on the mining timer
                worker.MiningTimer = goldMined;
            }
        }

        private void AdvanceGatherToBase(SimUnit worker)
        {
            // If base was destroyed, go idle
            if (!Units.TryGetValue(worker.GatherBaseNbr, out var baseUnit))
            {
                worker.CurrentAction = UnitAction.IDLE;
                worker.Path = null;
                return;
            }

            // Walk toward the base
            if (worker.Path != null && worker.PathIndex < worker.Path.Count)
            {
                MoveUnitOneStep(worker);
                worker.CurrentAction = UnitAction.GATHER;
                return;
            }

            // Arrived at base — deposit gold
            float goldCarried = worker.MiningTimer;
            Gold[worker.OwnerAgentNbr] += (int)goldCarried;

            // Cycle back to mine
            if (!Units.TryGetValue(worker.GatherMineNbr, out var mine) || mine.Health <= 0)
            {
                worker.CurrentAction = UnitAction.IDLE;
                return;
            }

            var path = Map.FindPathToUnit(worker.GridPosition, UnitType.MINE, mine.GridPosition);
            worker.GatherPhase = GatherPhase.TO_MINE;
            worker.Path = path;
            worker.PathIndex = 0;
        }

        private void AdvanceAttack(SimUnit attacker)
        {
            if (!Units.TryGetValue(attacker.AttackTargetNbr, out var target))
            {
                // Target dead or removed
                attacker.CurrentAction = UnitAction.IDLE;
                attacker.Path = null;
                return;
            }

            float range = GameConstants.ATTACK_RANGE[attacker.UnitType];
            float dist = Position.Distance(attacker.GridPosition, target.GridPosition);

            if (dist <= range + 0.1f)
            {
                // In range — deal damage
                float dmg = damage[attacker.UnitType] * Config.TickDuration;
                target.Health -= dmg;
            }
            else
            {
                // Out of range — move closer
                if (attacker.Path != null && attacker.PathIndex < attacker.Path.Count)
                {
                    MoveUnitOneStep(attacker);
                    attacker.CurrentAction = UnitAction.ATTACK;
                }
                else
                {
                    // Repath toward target (use FindPathToUnit for multi-cell buildings)
                    var path = Map.FindPathToUnit(attacker.GridPosition, target.UnitType, target.GridPosition);
                    if (path.Count > 0)
                    {
                        attacker.Path = path;
                        attacker.PathIndex = 0;
                    }
                }
            }
        }

        #endregion

        #region Helpers

        private void RemoveDeadUnits()
        {
            var dead = Units.Values.Where(u => u.Health <= 0 && u.UnitType != UnitType.MINE || (u.UnitType == UnitType.MINE && u.Health <= 0)).ToList();

            // Also remove mines that are depleted (health <= 0)
            foreach (var unit in dead)
            {
                Map.SetAreaBuildability(unit.UnitType, unit.GridPosition, true);
                Units.Remove(unit.UnitNbr);
            }
        }

        private bool IsAdjacentToUnit(Position pos, UnitType unitType, Position unitAnchor)
        {
            var neighbors = Map.GetPositionsNearUnit(unitType, unitAnchor);
            return neighbors.Any(n => n == pos);
        }

        #endregion
    }
}
