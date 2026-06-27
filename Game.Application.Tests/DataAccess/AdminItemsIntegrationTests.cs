using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises <see cref="IAdminItems"/> write paths that guard malformed change sets before they reach the
    /// database as a duplicate-key/FK violation or a silent 0-row update: the mod-slot existence/membership
    /// validation and the attribute change-set upsert. Seeding, the admin write, and the assertion each use a
    /// separate DI scope so the write runs against an empty change tracker, as a real admin call does.
    /// </summary>
    [Collection("Integration")]
    public class AdminItemsIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminItemsIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SaveItems_GrantedSkillNotItemFlagged_ReturnsFailure()
        {
            int skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                // A Player-only skill is not Item-acquirable, so it can't be granted by an item.
                var skill = await TestDataSeeder.CreateSkillAsync(context, "Player Bolt", acquisition: ESkillAcquisition.Player);
                skillId = skill.Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItems>();

            var result = admin.SaveItems(
            [
                new Change<Contracts.Item>
                {
                    ChangeType = EChangeType.Add,
                    Item = NewItem(name: "Bad Grant", grantedSkillId: skillId),
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal(
                "Skill 'Player Bolt' is not flagged as Item-acquirable and cannot be granted by an item.",
                result.ErrorMessage);
        }

        [Fact]
        public async Task SaveItems_GrantedSkillItemFlagged_Succeeds()
        {
            int skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var skill = await TestDataSeeder.CreateSkillAsync(context, "Cleave", acquisition: ESkillAcquisition.Item);
                skillId = skill.Id;
            }
            await ReloadReferenceCachesAsync();

            var changes = new List<Change<Contracts.Item>>
            {
                new() { ChangeType = EChangeType.Add, Item = NewItem(name: "Granting Axe", grantedSkillId: skillId) },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminItems>();
                Assert.True(admin.SaveItems(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var created = await context.Items.AsNoTracking().SingleAsync(i => i.Name == "Granting Axe", CancellationToken);
                Assert.Equal(skillId, created.GrantedSkillId);
            }
        }

        [Fact]
        public void SaveItems_UndefinedRarity_ReturnsFailureWithoutPersisting()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItems>();

            var item = NewItem(name: "Bad Rarity Item");
            item.RarityId = (ERarity)0;

            var result = admin.SaveItems([new Change<Contracts.Item> { ChangeType = EChangeType.Add, Item = item }]);

            Assert.False(result.Succeeded);
            Assert.Equal("0 is not a valid item rarity.", result.ErrorMessage);
        }

        [Fact]
        public void SaveItems_UndefinedCategory_ReturnsFailureWithoutPersisting()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItems>();

            var item = NewItem(name: "Bad Category Item");
            item.ItemCategoryId = (EItemCategory)0;

            var result = admin.SaveItems([new Change<Contracts.Item> { ChangeType = EChangeType.Add, Item = item }]);

            Assert.False(result.Succeeded);
            Assert.Equal("0 is not a valid item category.", result.ErrorMessage);
        }

        [Fact]
        public void SaveModSlots_AddForUnknownItem_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItems>();

            var result = admin.SaveModSlots(
            [
                new Change<Contracts.ItemModSlot>
                {
                    ChangeType = EChangeType.Add,
                    Item = new Contracts.ItemModSlot { ItemId = 99999, ItemModSlotTypeId = EItemModType.Prefix },
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Item not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveModSlots_AddsEditsAndDeletesAgainstItemMembership()
        {
            int itemId, editedSlotId, removedSlotId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var item = await TestDataSeeder.CreateItemAsync(context);
                var editedSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id, EItemModType.Prefix);
                var removedSlot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id, EItemModType.Suffix);

                itemId = item.Id;
                editedSlotId = editedSlot.Id;
                removedSlotId = removedSlot.Id;
            }
            await ReloadReferenceCachesAsync();

            // Add a new slot, retype the edited slot, and drop the removed slot.
            var changes = new List<Change<Contracts.ItemModSlot>>
            {
                new()
                {
                    ChangeType = EChangeType.Add,
                    Item = new Contracts.ItemModSlot { ItemId = itemId, ItemModSlotTypeId = EItemModType.Suffix },
                },
                new()
                {
                    ChangeType = EChangeType.Edit,
                    Item = new Contracts.ItemModSlot { Id = editedSlotId, ItemId = itemId, ItemModSlotTypeId = EItemModType.Suffix },
                },
                new()
                {
                    ChangeType = EChangeType.Delete,
                    Item = new Contracts.ItemModSlot { Id = removedSlotId, ItemId = itemId },
                },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminItems>();
                Assert.True(admin.SaveModSlots(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var slots = await context.ItemModSlots
                    .Where(s => s.ItemId == itemId)
                    .ToListAsync(CancellationToken);

                Assert.Equal(2, slots.Count);
                Assert.DoesNotContain(slots, s => s.Id == removedSlotId);
                Assert.Equal((int)EItemModType.Suffix, slots.Single(s => s.Id == editedSlotId).ItemModSlotTypeId);
            }
        }

        [Fact]
        public async Task SaveModSlots_EditAndDeleteOfNonMemberSlot_AreGuardedNoOps()
        {
            int itemId, slotId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var item = await TestDataSeeder.CreateItemAsync(context);
                var slot = await TestDataSeeder.AddItemModSlotAsync(context, item.Id, EItemModType.Prefix);

                itemId = item.Id;
                slotId = slot.Id;
            }
            await ReloadReferenceCachesAsync();

            // An edit/delete naming a slot id the item does not have must be reconciled away — not a silent
            // EF 0-row update/delete — leaving the real slot untouched.
            var changes = new List<Change<Contracts.ItemModSlot>>
            {
                new()
                {
                    ChangeType = EChangeType.Edit,
                    Item = new Contracts.ItemModSlot { Id = 99999, ItemId = itemId, ItemModSlotTypeId = EItemModType.Suffix },
                },
                new()
                {
                    ChangeType = EChangeType.Delete,
                    Item = new Contracts.ItemModSlot { Id = 88888, ItemId = itemId },
                },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminItems>();
                Assert.True(admin.SaveModSlots(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var slots = await context.ItemModSlots
                    .Where(s => s.ItemId == itemId)
                    .ToListAsync(CancellationToken);

                var slot = Assert.Single(slots);
                Assert.Equal(slotId, slot.Id);
                Assert.Equal((int)EItemModType.Prefix, slot.ItemModSlotTypeId);
            }
        }

        [Fact]
        public async Task SetAttributes_AddOfAlreadyPresentAttribute_Upserts()
        {
            int itemId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                // CreateItemAsync seeds a single Strength attribute (amount 5).
                var item = await TestDataSeeder.CreateItemAsync(context);
                itemId = item.Id;
            }
            await ReloadReferenceCachesAsync();

            // An Add of an attribute the item already has must upsert, not duplicate-insert into a
            // composite-PK violation at commit.
            var data = new AddEditAttributesData
            {
                Id = itemId,
                Changes =
                [
                    new Change<Contracts.BattlerAttribute>
                    {
                        ChangeType = EChangeType.Add,
                        Item = new Contracts.BattlerAttribute { AttributeId = EAttribute.Strength, Amount = 50m },
                    },
                ],
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminItems>();
                Assert.True(admin.SetAttributes(data).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var attributes = await context.ItemAttributes
                    .Where(a => a.ItemId == itemId)
                    .ToListAsync(CancellationToken);

                var attribute = Assert.Single(attributes);
                Assert.Equal((int)EAttribute.Strength, attribute.AttributeId);
                Assert.Equal(50m, attribute.Amount);
            }
        }

        [Fact]
        public async Task SetAttributes_DuplicateKeyInBatch_ReturnsFailureWithoutThrowing()
        {
            int itemId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var item = await TestDataSeeder.CreateItemAsync(context);
                itemId = item.Id;
            }
            await ReloadReferenceCachesAsync();

            // Two Adds of the same attribute would double-track the composite key and surface as an opaque EF
            // 500 mid-batch; the processor must reject the malformed batch up front as a graceful failure.
            var data = new AddEditAttributesData
            {
                Id = itemId,
                Changes =
                [
                    new Change<Contracts.BattlerAttribute>
                    {
                        ChangeType = EChangeType.Add,
                        Item = new Contracts.BattlerAttribute { AttributeId = EAttribute.Agility, Amount = 1m },
                    },
                    new Change<Contracts.BattlerAttribute>
                    {
                        ChangeType = EChangeType.Add,
                        Item = new Contracts.BattlerAttribute { AttributeId = EAttribute.Agility, Amount = 2m },
                    },
                ],
            };

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItems>();

            var result = admin.SetAttributes(data);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted item attribute change set contains duplicate entries.", result.ErrorMessage);
        }

        [Fact]
        public void SaveItems_RequiredProficiencyDoesNotExist_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItems>();

            var result = admin.SaveItems(
            [
                new Change<Contracts.Item>
                {
                    ChangeType = EChangeType.Add,
                    Item = NewItem(name: "Bad Gate", requiredProficiencyId: 99999, requiredProficiencyLevel: 3),
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Required proficiency 99999 does not exist.", result.ErrorMessage);
        }

        [Theory]
        [InlineData(0)]   // a gate of level 0 every player trivially meets — not a real requirement
        [InlineData(11)]  // above the proficiency's MaxLevel — unsatisfiable
        public async Task SaveItems_RequiredProficiencyLevelOutOfRange_ReturnsFailure(int level)
        {
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, "Fire Magic", maxLevel: 10);
                proficiencyId = proficiency.Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItems>();

            var result = admin.SaveItems(
            [
                new Change<Contracts.Item>
                {
                    ChangeType = EChangeType.Add,
                    Item = NewItem(name: "Out Of Range", requiredProficiencyId: proficiencyId, requiredProficiencyLevel: level),
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal(
                $"Required proficiency level {level} is outside the valid range for 'Fire Magic' (1 to 10).",
                result.ErrorMessage);
        }

        [Fact]
        public async Task SaveItems_RequiredProficiencyRetired_ReturnsFailure()
        {
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, "Old Magic");
                proficiency.RetiredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                await context.SaveChangesAsync(CancellationToken);
                proficiencyId = proficiency.Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminItems>();

            var result = admin.SaveItems(
            [
                new Change<Contracts.Item>
                {
                    ChangeType = EChangeType.Add,
                    Item = NewItem(name: "Retired Gate", requiredProficiencyId: proficiencyId, requiredProficiencyLevel: 3),
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Proficiency 'Old Magic' is retired and cannot gate an item.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveItems_ValidProficiencyGate_PersistsIdAndLevel()
        {
            int proficiencyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var proficiency = await TestDataSeeder.CreateProficiencyAsync(context, "Swordsmanship", maxLevel: 10);
                proficiencyId = proficiency.Id;
            }
            await ReloadReferenceCachesAsync();

            var changes = new List<Change<Contracts.Item>>
            {
                new()
                {
                    ChangeType = EChangeType.Add,
                    Item = NewItem(name: "Gated Blade", requiredProficiencyId: proficiencyId, requiredProficiencyLevel: 5),
                },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminItems>();
                Assert.True(admin.SaveItems(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var created = await context.Items.AsNoTracking().SingleAsync(i => i.Name == "Gated Blade", CancellationToken);
                Assert.Equal(proficiencyId, created.RequiredProficiencyId);
                Assert.Equal(5, created.RequiredProficiencyLevel);
            }
        }

        [Fact]
        public async Task SaveItems_UngatedItem_PersistsNullGateAndZeroLevel()
        {
            // An ungated item carries no proficiency id; a stray level is coerced to 0 rather than persisted.
            var changes = new List<Change<Contracts.Item>>
            {
                new()
                {
                    ChangeType = EChangeType.Add,
                    Item = NewItem(name: "Ungated Trinket", requiredProficiencyId: null, requiredProficiencyLevel: 7),
                },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminItems>();
                Assert.True(admin.SaveItems(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var created = await context.Items.AsNoTracking().SingleAsync(i => i.Name == "Ungated Trinket", CancellationToken);
                Assert.Null(created.RequiredProficiencyId);
                Assert.Equal(0, created.RequiredProficiencyLevel);
            }
        }

        private static Contracts.Item NewItem(int id = 0, string name = "Test Item", int? grantedSkillId = null,
            int? requiredProficiencyId = null, int requiredProficiencyLevel = 0) => new()
            {
                Id = id,
                Name = name,
                Description = "",
                ItemCategoryId = EItemCategory.Weapon,
                RarityId = ERarity.Common,
                IconPath = "",
                GrantedSkillId = grantedSkillId,
                RequiredProficiencyId = requiredProficiencyId,
                RequiredProficiencyLevel = requiredProficiencyLevel,
                Attributes = [],
                ModSlots = [],
                Tags = [],
            };
    }
}
