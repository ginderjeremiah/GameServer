using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameInfrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ZoneEnemiesProbTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
			SET ANSI_NULLS ON
			GO

			SET QUOTED_IDENTIFIER ON
			GO

			CREATE TRIGGER [dbo].[trig_ZoneEnemies_ProbabilityRecalc] ON [dbo].[ZoneEnemies]
			AFTER INSERT, UPDATE, DELETE
			AS BEGIN
				SET NOCOUNT ON

				--Get affected zones
				SELECT ZoneId INTO #ZoneIds 
				FROM deleted
				UNION
				SELECT ZoneId
				FROM inserted

				--Clear out existing probabilities
				DELETE ZEP
				FROM ZoneEnemyProbabilities AS ZEP
				INNER JOIN ZoneEnemies AS ZE ON ZE.Id = ZEP.ZoneEnemyId
				WHERE ZE.ZoneId IN (SELECT ZoneId FROM #ZoneIds)

				--Clear out existing aliases
				DELETE ZEA
				FROM ZoneEnemyAliases AS ZEA
				INNER JOIN ZoneEnemies AS ZE ON ZE.Id = ZEA.ZoneEnemyId
				WHERE ZE.ZoneId IN (SELECT ZoneId FROM #ZoneIds)

				DECLARE @SmallZoneEnemyId INT, --id of current enemy in Small List
					@SmallNormalizedProb DEC(9, 8), --normalized probability [0, 1) of current enemy in Small list (relative to avg probabality)
					@BigZoneEnemyId INT, -- id of current enemy in Big List
					@BigNormalizedProb DEC(9, 8), --normalized probability (1, inf) of current enemy in Big list (relative to avg probabality)
					@ZoneId INT, --current zone calculating for
					@WeightAvg DEC(18, 9), --Average weight for current zone
					@RowNum INT --row number for a calculated probability in its respective zone.  Used to grab a random row for a specific zone.

				--DECLARE @EnemiesInZone INT,
				--	@ZoneProbInserts INT

				--Calculate probability averages for each zone
				SELECT ZoneId, AVG(CONVERT(DEC(18, 9), Weight)) AS WeightAvg
				INTO #ZoneProbabilityAvgs
				FROM ZoneEnemies
				WHERE ZoneId IN (SELECT ZoneId FROM #ZoneIds)
				GROUP BY ZoneId

				--Temp Table to hold enemies with a weight < Avg for zone
				CREATE TABLE #Small (
					ZoneEnemyId INT,
					NormalizedProb DEC(18, 9)
				)

				--Temp Table to hold enemies with a weight > Avg for zone
				CREATE TABLE #Big (
					ZoneEnemyId INT,
					NormalizedProb DEC(18, 9)
				)

				DECLARE ZoneCursor CURSOR FORWARD_ONLY
				FOR SELECT * FROM #ZoneProbabilityAvgs

				OPEN ZoneCursor

				FETCH NEXT FROM ZoneCursor
				INTO @ZoneId, @WeightAvg

				WHILE @@FETCH_STATUS = 0
				BEGIN
					--PRINT 'Zone Cursor Loop for id: ' + CONVERT(VARCHAR(10), @ZoneId) + ', weight: ' + CONVERT(VARCHAR(40), @WeightAvg)

					--Clear out values from Small and Big lists
					TRUNCATE TABLE #Small
					TRUNCATE TABLE #Big
		
					--Reset Big ZoneEnemy Id
					SELECT @BigZoneEnemyId = NULL--,
					--@ZoneProbInserts = 0,
					--@EnemiesInZone = (SELECT COUNT(*) FROM ZoneEnemies WHERE ZoneEnemies.ZoneId = @ZoneId)

					--Calculate normalized probabilty for enemies with weight < weightAvg
					INSERT INTO #Small
					SELECT ZE.Id, CONVERT(DEC(18, 9), Weight) / @WeightAvg AS NormalizedProb
					FROM ZoneEnemies AS ZE
					WHERE CONVERT(DEC(18, 9), Weight) < @WeightAvg AND ZE.ZoneId = @ZoneId

					--PRINT 'INSERT into #Small: ' + CONVERT(VARCHAR(10), @@ROWCOUNT)

					--Calculate normalized probabilty for enemies with weight > weightAvg
					INSERT INTO #Big
					SELECT ZE.Id, CONVERT(DEC(18, 9), Weight) / @WeightAvg AS NormalizedProb
					FROM ZoneEnemies AS ZE
					WHERE CONVERT(DEC(18, 9), Weight) > @WeightAvg AND ZE.ZoneId = @ZoneId

					--PRINT 'INSERT into #Big: ' + CONVERT(VARCHAR(10), @@ROWCOUNT)

					--Insert all probabilities equal to average into probabilities table
					INSERT INTO ZoneEnemyProbabilities
					SELECT ZE.Id, 1, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1
					FROM ZoneEnemies AS ZE
					WHERE CONVERT(DEC(18, 9), Weight) = @WeightAvg AND ZE.ZoneId = @ZoneId
		
					--Set @RowNum to next number
					SELECT @RowNum = @@ROWCOUNT

					--SELECT @ZoneProbInserts = @ZoneProbInserts + @@ROWCOUNT

					DECLARE SmallCursor CURSOR FORWARD_ONLY
					FOR SELECT * FROM #Small

					OPEN SmallCursor

					--Get first Small probability
					FETCH NEXT FROM SmallCursor
					INTO @SmallZoneEnemyId, @SmallNormalizedProb

					--Select first enemy in #Big to pair with first enemy from #Small
					SELECT TOP(1) @BigZoneEnemyId = ZoneEnemyId, @BigNormalizedProb = NormalizedProb
					FROM #Big

					WHILE @@FETCH_STATUS = 0
					BEGIN
						--PRINT 'Start Small Cursor Loop '

						--Remove portion of Big probability to give to Small probability
						SELECT @BigNormalizedProb = @BigNormalizedProb + @SmallNormalizedProb - 1;

						--Add new entry in probabilities table for Small probability
						INSERT INTO ZoneEnemyProbabilities
						SELECT @SmallZoneEnemyId, @SmallNormalizedProb, @RowNum

						--SELECT @ZoneProbInserts = @ZoneProbInserts + @@ROWCOUNT

						--Add Alias for Small and Big
						INSERT INTO ZoneEnemyAliases
						SELECT @SmallZoneEnemyId, @BigZoneEnemyId

						--Get next Small probability
						FETCH NEXT FROM SmallCursor
						INTO @SmallZoneEnemyId, @SmallNormalizedProb

						--Check if Big probability is now less than one and needs to go to small list and whether small list is done (for edge case)
						IF @BigNormalizedProb < 1 AND @@FETCH_STATUS = 0
						BEGIN
							--Insert Big probability into Small
							INSERT INTO #Small
							SELECT @BigZoneEnemyID, @BigNormalizedProb

							--Remove Big probabiliy from Big
							DELETE FROM #Big
							WHERE #Big.ZoneEnemyId = @BigZoneEnemyId

							--Get next Big probability
							SELECT TOP(1) @BigZoneEnemyId = ZoneEnemyId, @BigNormalizedProb = NormalizedProb
							FROM #Big
						END
						--ELSE
						--BEGIN
						--	PRINT 'Small Cursor IF not triggered: ' + CAST(@BigNormalizedProb AS VARCHAR(30)) + ', ' + CAST(@@FETCH_STATUS AS VARCHAR(2))
						--END

						--Increment @RowNum
						SELECT @RowNum = @RowNum + 1

						--PRINT 'End Small Cursor Loop'
					END

					CLOSE SmallCursor
					DEALLOCATE SmallCursor

					--PRINT 'Big Zone Enemy Id'
					--PRINT @BigZoneEnemyId
					--Insert last Big probability into probabilities table
					IF @BigZoneEnemyId IS NOT NULL
					BEGIN
						INSERT INTO ZoneEnemyProbabilities
						SELECT @BigZoneEnemyId, 1, @RowNum

						--SELECT @ZoneProbInserts = @ZoneProbInserts + @@ROWCOUNT
					END

					--PRINT 'End Zone Cursor Loop for id: ' + CONVERT(VARCHAR(10), @ZoneId) + ', weight: ' + CONVERT(VARCHAR(40), @WeightAvg)
					--PRINT 'Number of enemies ' + CAST(@EnemiesInZone AS VARCHAR(20))
					--PRINT 'Number of inserts ' + CAST(@ZoneProbInserts AS VARCHAR(20))

					--Get next zone to calculate probabilities for
					FETCH NEXT FROM ZoneCursor
					INTO @ZoneId, @WeightAvg

				END

				CLOSE ZoneCursor
				DEALLOCATE ZoneCursor
			END

			ALTER TABLE [dbo].[ZoneEnemies] ENABLE TRIGGER [trig_ZoneEnemies_ProbabilityRecalc]
			GO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER [dbo].[trig_ZoneEnemies_ProbabilityRecalc]");
        }
    }
}
