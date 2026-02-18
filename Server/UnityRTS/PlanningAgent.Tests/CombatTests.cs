using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Tests for the combat system: attacking, damage, death, validation.
    /// </summary>
    public class CombatTests
    {
        // ------------------------------------------------------------------
        // Happy path
        // ------------------------------------------------------------------

        [Fact]
        public void SoldierAttacksEnemy_EnemyHealthDecreases()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.SOLDIER, new Position(10, 10))
                .WithUnit(1, UnitType.SOLDIER, new Position(11, 10))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            float healthBefore = game.GetUnitsByType(1, UnitType.SOLDIER)[0].Health;
            game.Run(20);

            var enemySoldiers = game.GetUnitsByType(1, UnitType.SOLDIER);
            if (enemySoldiers.Count > 0)
            {
                Assert.True(enemySoldiers[0].Health < healthBefore,
                    $"Enemy health should decrease. Before: {healthBefore}, After: {enemySoldiers[0].Health}");
            }
            // If enemy is dead, that's also a valid outcome
        }

        [Fact]
        public void SoldierKillsEnemy_EnemyRemoved()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.SOLDIER, new Position(10, 10))
                .WithUnit(1, UnitType.WORKER, new Position(11, 10)) // Low health target
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            // Worker (50 HP) should be dead
            Assert.Empty(game.GetUnitsByType(1, UnitType.WORKER));
        }

        [Fact]
        public void AttackerGoesIdle_WhenTargetDies()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.SOLDIER, new Position(10, 10))
                .WithUnit(1, UnitType.WORKER, new Position(11, 10))
                .WithAgent(0, new AttackOnceAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            var soldiers = game.GetUnitsByType(0, UnitType.SOLDIER);
            Assert.Single(soldiers);
            Assert.Equal(UnitAction.IDLE, soldiers[0].CurrentAction);
        }

        [Fact]
        public void ArcherAttacks_DealsLessDamage()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.ARCHER, new Position(10, 10))
                .WithUnit(1, UnitType.BASE, new Position(12, 10), isBuilt: true)
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            float baseBefore = game.GetUnitsByType(1, UnitType.BASE)[0].Health;
            game.Run(50);

            var bases = game.GetUnitsByType(1, UnitType.BASE);
            Assert.Single(bases);
            // Archer damage (3 * 20 = 60 dps) is much less than soldier (20 * 20 = 400 dps)
            // BASE has 1000 HP, so archer shouldn't kill it in 50 ticks
            Assert.True(bases[0].Health < baseBefore);
            Assert.True(bases[0].Health > 0, "BASE should still be alive — archer DPS is low");
        }

        [Fact]
        public void SoldierWalksToTarget_ThenAttacks()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.SOLDIER, new Position(5, 5))
                .WithUnit(1, UnitType.WORKER, new Position(15, 5))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            // Soldier should have walked over and killed the worker
            Assert.Empty(game.GetUnitsByType(1, UnitType.WORKER));
        }

        // ------------------------------------------------------------------
        // Error cases
        // ------------------------------------------------------------------

        [Fact]
        public void AttackOwnUnit_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.SOLDIER, new Position(10, 10))
                .WithUnit(0, UnitType.WORKER, new Position(11, 10))
                .WithAgent(0, new AttackOwnWorkerAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            // Own worker should still be alive
            var workers = game.GetUnitsByType(0, UnitType.WORKER);
            Assert.Single(workers);
            Assert.Equal(GameConstants.HEALTH[UnitType.WORKER], workers[0].Health);
        }

        [Fact]
        public void WorkerCannotAttack()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WORKER, new Position(10, 10))
                .WithUnit(1, UnitType.WORKER, new Position(11, 10))
                .WithAgent(0, new WorkerAttackAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(50);

            // Enemy worker should be unharmed
            var enemies = game.GetUnitsByType(1, UnitType.WORKER);
            Assert.Single(enemies);
            Assert.Equal(GameConstants.HEALTH[UnitType.WORKER], enemies[0].Health);
        }

        [Fact]
        public void AttackMine_Rejected()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.SOLDIER, new Position(10, 10))
                .WithMine(new Position(12, 10), health: 10000)
                .WithAgent(0, new AttackMineAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            var mines = game.GetUnitsByType(-1, UnitType.MINE);
            Assert.Single(mines);
            Assert.Equal(10000, mines[0].Health);
        }

        // ------------------------------------------------------------------
        // Boundary: destroyed unit frees cells
        // ------------------------------------------------------------------

        [Fact]
        public void DestroyedBuilding_FreesCells()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.SOLDIER, new Position(10, 10))
                .WithUnit(0, UnitType.SOLDIER, new Position(10, 11))
                .WithUnit(0, UnitType.SOLDIER, new Position(10, 12))
                .WithUnit(1, UnitType.BARRACKS, new Position(12, 11), isBuilt: true)
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            // Barracks has 500 HP, soldiers do 400 dps each
            game.InitializeMatch();
            game.InitializeRound();
            game.Run(200);

            Assert.Empty(game.GetUnitsByType(1, UnitType.BARRACKS));

            // Cells under the barracks footprint should be free
            Assert.True(game.Map.IsPositionBuildable(new Position(12, 11)));
            Assert.True(game.Map.IsPositionBuildable(new Position(13, 11)));
        }

        [Fact]
        public void DestroyedWorker_FreesCell()
        {
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.SOLDIER, new Position(10, 10))
                .WithUnit(1, UnitType.WORKER, new Position(11, 10))
                .WithAgent(0, new AttackFirstEnemyAgent())
                .Build();

            var workerPos = game.GetUnitsByType(1, UnitType.WORKER)[0].GridPosition;

            game.InitializeMatch();
            game.InitializeRound();
            game.Run(100);

            Assert.Empty(game.GetUnitsByType(1, UnitType.WORKER));
            Assert.True(game.Map.IsPositionBuildable(workerPos));
        }

        // ------------------------------------------------------------------
        // Stress test
        // ------------------------------------------------------------------

        [Fact]
        public void ManyUnitsInCombat_NoExceptions()
        {
            var builder = new SimGameBuilder()
                .WithMapSize(30, 30);

            // 10 soldiers per side
            for (int i = 0; i < 10; i++)
            {
                builder.WithUnit(0, UnitType.SOLDIER, new Position(5, 2 + i));
                builder.WithUnit(1, UnitType.SOLDIER, new Position(25, 2 + i));
            }

            var game = builder
                .WithAgent(0, new AttackAllEnemiesAgent())
                .WithAgent(1, new AttackAllEnemiesAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Should run without exceptions
            game.Run(500);

            // One side should have fewer soldiers (combat happened)
            int total = game.GetUnitsByType(0, UnitType.SOLDIER).Count
                      + game.GetUnitsByType(1, UnitType.SOLDIER).Count;
            Assert.True(total < 20, $"Some soldiers should have died. Total remaining: {total}");
        }
    }

    // ------------------------------------------------------------------
    // Test helper agents
    // ------------------------------------------------------------------

    internal class AttackFirstEnemyAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            // Find any enemy unit
            int? targetNbr = null;
            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) { targetNbr = enemies[0]; break; }
            }

            if (!targetNbr.HasValue) return;

            // All my attack-capable units attack that target
            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER })
            {
                foreach (int unitNbr in state.GetMyUnits(ut))
                {
                    var info = state.GetUnit(unitNbr);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                        actions.Attack(unitNbr, targetNbr.Value);
                }
            }
        }
    }

    internal class AttackOnceAgent : IPlanningAgent
    {
        private bool attacked;
        public void InitializeMatch() { attacked = false; }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            if (attacked) return;
            var soldiers = state.GetMyUnits(UnitType.SOLDIER);
            var enemies = state.GetEnemyUnits(UnitType.WORKER);
            if (soldiers.Count > 0 && enemies.Count > 0)
            {
                actions.Attack(soldiers[0], enemies[0]);
                attacked = true;
            }
        }
    }

    internal class AttackOwnWorkerAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var soldiers = state.GetMyUnits(UnitType.SOLDIER);
            var workers = state.GetMyUnits(UnitType.WORKER);
            if (soldiers.Count > 0 && workers.Count > 0)
                actions.Attack(soldiers[0], workers[0]);
        }
    }

    internal class WorkerAttackAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var workers = state.GetMyUnits(UnitType.WORKER);
            var enemies = state.GetEnemyUnits(UnitType.WORKER);
            if (workers.Count > 0 && enemies.Count > 0)
                actions.Attack(workers[0], enemies[0]);
        }
    }

    internal class AttackMineAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            var soldiers = state.GetMyUnits(UnitType.SOLDIER);
            var mines = state.GetAllUnits(UnitType.MINE);
            if (soldiers.Count > 0 && mines.Count > 0)
                actions.Attack(soldiers[0], mines[0]);
        }
    }

    internal class AttackAllEnemiesAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            int? target = null;
            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) { target = enemies[0]; break; }
            }

            if (!target.HasValue) return;

            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER })
            {
                foreach (int unitNbr in state.GetMyUnits(ut))
                {
                    var info = state.GetUnit(unitNbr);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                        actions.Attack(unitNbr, target.Value);
                }
            }
        }
    }
}
