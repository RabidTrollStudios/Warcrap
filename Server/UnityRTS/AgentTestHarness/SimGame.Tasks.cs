using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Task advancement: processes in-progress unit actions each tick
    /// (move, train, build, gather, attack).
    /// </summary>
    public partial class SimGame
    {
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

        #region Movement

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

        #endregion

        #region Training

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

        #endregion

        #region Building

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

        #endregion

        #region Gathering

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

        #endregion

        #region Combat

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
    }
}
