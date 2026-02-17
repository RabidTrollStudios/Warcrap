using AgentSDK;
using GameManager.GameElements;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameManager
{
    /// <summary>
    /// Implements IGameState by wrapping the game engine's managers.
    /// Converts between Unity types (Vector3Int) and SDK types (Position).
    /// </summary>
    public class GameStateAdapter : IGameState
    {
        private int agentNbr;
        private int enemyAgentNbr;
        private UnitManager unitManager;
        private MapManager mapManager;

        public GameStateAdapter(int agentNbr, UnitManager unitManager, MapManager mapManager)
        {
            this.agentNbr = agentNbr;
            this.unitManager = unitManager;
            this.mapManager = mapManager;
        }

        internal void UpdateEnemyAgentNbr(int enemyNbr)
        {
            this.enemyAgentNbr = enemyNbr;
        }

        public int MyAgentNbr => agentNbr;
        public int EnemyAgentNbr => enemyAgentNbr;
        public int MyGold => GameManager.Instance.GetAgent(agentNbr).Gold;
        public int EnemyGold => enemyAgentNbr >= 0 ? GameManager.Instance.GetAgent(enemyAgentNbr).Gold : 0;
        public Position MapSize => new Position(mapManager.MapSize.x, mapManager.MapSize.y);
        public int MyWins => GameManager.Instance.GetAgent(agentNbr).AgentNbrWins;

        public IReadOnlyList<int> GetMyUnits(AgentSDK.UnitType unitType)
            => unitManager.GetUnitNbrsOfType(unitType, agentNbr);

        public IReadOnlyList<int> GetEnemyUnits(AgentSDK.UnitType unitType)
            => enemyAgentNbr >= 0 ? unitManager.GetUnitNbrsOfType(unitType, enemyAgentNbr) : new List<int>();

        public IReadOnlyList<int> GetAllUnits(AgentSDK.UnitType unitType)
            => unitManager.GetUnitNbrsOfType(unitType);

        public UnitInfo? GetUnit(int unitNbr)
        {
            var unit = unitManager.GetUnit(unitNbr);
            if (unit == null) return null;

            int ownerNbr = unit.Agent.GetComponent<AgentController>().Agent.AgentNbr;
            return new UnitInfo(
                unit.UnitNbr,
                unit.UnitType,
                new Position(unit.GridPosition.x, unit.GridPosition.y),
                unit.Health,
                unit.IsBuilt,
                unit.CurrentAction,
                unit.CanMove,
                unit.CanBuild,
                unit.CanTrain,
                unit.CanAttack,
                unit.CanGather,
                ownerNbr
            );
        }

        public bool IsPositionBuildable(Position pos)
            => mapManager.IsGridPositionBuildable(new Vector3Int(pos.X, pos.Y, 0));

        public bool IsAreaBuildable(AgentSDK.UnitType unitType, Position pos)
            => mapManager.IsAreaBuildable(unitType, new Vector3Int(pos.X, pos.Y, 0));

        public bool IsBoundedAreaBuildable(AgentSDK.UnitType unitType, Position pos)
            => mapManager.IsBoundedAreaBuildable(unitType, new Vector3Int(pos.X, pos.Y, 0));

        public IReadOnlyList<Position> GetPathBetween(Position start, Position end)
        {
            var path = mapManager.GetPathBetweenGridPositions(
                new Vector3Int(start.X, start.Y, 0),
                new Vector3Int(end.X, end.Y, 0));
            return path.Select(p => new Position(p.x, p.y)).ToList();
        }

        public IReadOnlyList<Position> GetPathToUnit(Position start, AgentSDK.UnitType unitType, Position unitPos)
        {
            var path = mapManager.GetPathToUnit(
                new Vector3Int(start.X, start.Y, 0),
                unitType,
                new Vector3Int(unitPos.X, unitPos.Y, 0));
            return path.Select(p => new Position(p.x, p.y)).ToList();
        }

        public IReadOnlyList<Position> GetBuildablePositionsNearUnit(AgentSDK.UnitType unitType, Position unitPos)
        {
            var positions = mapManager.GetBuildableGridPositionsNearUnit(
                unitType, new Vector3Int(unitPos.X, unitPos.Y, 0));
            return positions.Select(p => new Position(p.x, p.y)).ToList();
        }

        public IReadOnlyList<Position> FindProspectiveBuildPositions(AgentSDK.UnitType unitType)
        {
            var result = new List<Position>();
            for (int i = 0; i < mapManager.MapSize.x; ++i)
            {
                for (int j = 0; j < mapManager.MapSize.y; ++j)
                {
                    var testPos = new Vector3Int(i, j, 0);
                    if (Utility.IsValidGridLocation(testPos)
                        && mapManager.IsBoundedAreaBuildable(unitType, testPos))
                    {
                        result.Add(new Position(i, j));
                    }
                }
            }
            return result;
        }
    }
}
