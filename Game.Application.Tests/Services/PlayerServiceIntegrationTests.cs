using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Application.Services;
using Game.Core;
using Game.Core.Attributes.Modifiers;
using Game.Core.Players;
using Game.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Application.Tests.Services
{
    [Collection("Integration")]
    public class PlayerServiceIntegrationTests : ApplicationIntegrationTestBase
    {
        public PlayerServiceIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task TryUpdateAttributes_ValidUpdate_ReturnsTrue()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            // Give the player 6 unspent stat points
            playerEntity.StatPointsGained = 106;
            await context.SaveChangesAsync(CancellationToken);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

            var updates = new List<SimpleAttributeUpdate>
            {
                new(EAttribute.Strength, Amount: 3),
            };

            var result = await playerService.TryUpdateAttributes(player, updates);

            Assert.True(result);
        }

        [Fact]
        public async Task TryUpdateAttributes_SpendMoreThanAvailable_ReturnsFalse()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

            var updates = new List<SimpleAttributeUpdate>
            {
                new(EAttribute.Strength, Amount: 999),
            };

            var result = await playerService.TryUpdateAttributes(player, updates);

            Assert.False(result);
        }

        [Fact]
        public async Task LoadPlayer_ExistingPlayer_LoadsFromDbThenCache()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

            var player1 = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player1);
            Assert.Equal(playerEntity.Name, player1.Name);

            var player2 = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player2);
            Assert.Equal(player1.Name, player2.Name);
        }

        [Fact]
        public async Task LoadPlayer_NonExistent_ReturnsNull()
        {
            using var scope = CreateScope();
            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

            var player = await playerService.LoadPlayer(99999);

            Assert.Null(player);
        }

        [Fact]
        public async Task ApplyMod_EquippedItem_AddsModAttributesToEquippedModifiers()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // A weapon (base Strength +5) with one Prefix mod slot, equipped in the weapon slot.
            var item = await TestDataSeeder.CreateItemAsync(context);
            context.ItemModSlots.Add(new Infrastructure.Entities.ItemModSlot
            {
                ItemId = item.Id,
                ItemModSlotTypeId = (int)EItemModType.Prefix,
                Index = 0,
            });
            context.UnlockedItems.Add(new Infrastructure.Entities.UnlockedItem
            {
                PlayerId = playerEntity.Id,
                ItemId = item.Id,
                EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot,
            });

            // A Prefix mod granting Dexterity +7, unlocked for the player.
            var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
            context.UnlockedMods.Add(new Infrastructure.Entities.UnlockedMod
            {
                PlayerId = playerEntity.Id,
                ItemModId = mod.Id,
            });
            await context.SaveChangesAsync(CancellationToken);

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var applied = await playerService.ApplyMod(player, item.Id, mod.Id, itemModSlotId: 0);

            Assert.True(applied);
            var modifiers = player.Inventory.GetEquippedAttributeModifiers().ToList();
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Strength && m.Amount == 5.0 && m.Source == EAttributeModifierSource.Item);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Dexterity && m.Amount == 7.0 && m.Source == EAttributeModifierSource.ItemMod);
        }

        [Fact]
        public async Task ApplyMod_NonexistentMod_ReturnsFalse()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // A mod id far beyond any seeded catalog entry: ApplyMod must reject it (returning false)
            // rather than throwing when it resolves the mod for application.
            var applied = await playerService.ApplyMod(player, itemId: 0, itemModId: 99999, itemModSlotId: 0);

            Assert.False(applied);
        }

        [Fact]
        public async Task LoadPlayer_PersistedAppliedMod_IncludesModAttributesInEquippedModifiers()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // A weapon (base Strength +5) with one Prefix mod slot, equipped in the weapon slot.
            var item = await TestDataSeeder.CreateItemAsync(context);
            var modSlot = new Infrastructure.Entities.ItemModSlot
            {
                ItemId = item.Id,
                ItemModSlotTypeId = (int)EItemModType.Prefix,
                Index = 0,
            };
            context.ItemModSlots.Add(modSlot);
            context.UnlockedItems.Add(new Infrastructure.Entities.UnlockedItem
            {
                PlayerId = playerEntity.Id,
                ItemId = item.Id,
                EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot,
            });

            // A Prefix mod granting Dexterity +7, unlocked for the player.
            var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
            context.UnlockedMods.Add(new Infrastructure.Entities.UnlockedMod
            {
                PlayerId = playerEntity.Id,
                ItemModId = mod.Id,
            });
            await context.SaveChangesAsync(CancellationToken);

            // Persist the applied mod as if it had been applied in a previous session, so the
            // player is loaded fresh from the DB with it already in place (the #90 scenario).
            context.AppliedMods.Add(new Infrastructure.Entities.AppliedMod
            {
                PlayerId = playerEntity.Id,
                ItemId = item.Id,
                ItemModSlotId = modSlot.Id,
                ItemModId = mod.Id,
            });
            await context.SaveChangesAsync(CancellationToken);

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var modifiers = player.Inventory.GetEquippedAttributeModifiers().ToList();
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Strength && m.Amount == 5.0 && m.Source == EAttributeModifierSource.Item);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Dexterity && m.Amount == 7.0 && m.Source == EAttributeModifierSource.ItemMod);
        }

        [Fact]
        public async Task LoadPlayer_ResolvesReferenceDataFromCache()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Reference data (skill, item with attribute + mod slot, mod) authored independently of
            // the player; PlayerRepository must stitch these in from the in-memory catalogs.
            var skill = await TestDataSeeder.CreateSkillAsync(context, name: "Fireball", baseDamage: 12m, cooldownMs: 1500);
            var item = await TestDataSeeder.CreateItemAsync(context, name: "Sword", attributeId: EAttribute.Strength, attributeAmount: 5m);
            var modSlot = new Infrastructure.Entities.ItemModSlot
            {
                ItemId = item.Id,
                ItemModSlotTypeId = (int)EItemModType.Prefix,
                Index = 0,
            };
            context.ItemModSlots.Add(modSlot);
            var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id, selected: true);
            context.UnlockedItems.Add(new Infrastructure.Entities.UnlockedItem
            {
                PlayerId = playerEntity.Id,
                ItemId = item.Id,
                EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot,
            });
            context.UnlockedMods.Add(new Infrastructure.Entities.UnlockedMod
            {
                PlayerId = playerEntity.Id,
                ItemModId = mod.Id,
            });
            context.AppliedMods.Add(new Infrastructure.Entities.AppliedMod
            {
                PlayerId = playerEntity.Id,
                ItemId = item.Id,
                ItemModSlotId = modSlot.Id,
                ItemModId = mod.Id,
            });
            await context.SaveChangesAsync(CancellationToken);

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // Skill definition resolved from the cached catalog
            var loadedSkill = Assert.Single(player.Skills);
            Assert.Equal("Fireball", loadedSkill.Name);
            Assert.Equal(12.0, loadedSkill.BaseDamage);
            Assert.Equal(1500, loadedSkill.CooldownMs);
            Assert.NotEmpty(loadedSkill.DamageMultipliers);
            // Skills and SelectedSkills share the same resolved instance (immutable template data)
            var selectedSkill = Assert.Single(player.SelectedSkills);
            Assert.Same(loadedSkill, selectedSkill);

            // Item definition (with its attributes and mod slots) resolved from the cached catalog
            var unlocked = Assert.Single(player.Inventory.UnlockedItems);
            Assert.Equal("Sword", unlocked.Item.Name);
            Assert.Contains(unlocked.Item.Attributes, a => a.Attribute == EAttribute.Strength && a.Amount == 5.0);
            Assert.NotEmpty(unlocked.Item.ModSlots);

            // Applied mod resolved with its attributes, and folded into the equipped modifiers
            var appliedMod = Assert.Single(unlocked.AppliedMods);
            Assert.Equal(mod.Id, appliedMod.ItemModId);
            Assert.Contains(appliedMod.ItemMod.Attributes, a => a.Attribute == EAttribute.Dexterity && a.Amount == 7.0);

            var modifiers = player.Inventory.GetEquippedAttributeModifiers().ToList();
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Strength && m.Amount == 5.0 && m.Source == EAttributeModifierSource.Item);
            Assert.Contains(modifiers, m => m.Attribute == EAttribute.Dexterity && m.Amount == 7.0 && m.Source == EAttributeModifierSource.ItemMod);
        }

        [Fact]
        public async Task LoadPlayer_OrdersSelectedSkillsByOrderThenSkillId()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // CreateSkillAsync assigns sequential ids, so these five skills have ascending ids.
            var skillA = await TestDataSeeder.CreateSkillAsync(context, name: "A");
            var skillB = await TestDataSeeder.CreateSkillAsync(context, name: "B");
            var skillC = await TestDataSeeder.CreateSkillAsync(context, name: "C");
            var skillD = await TestDataSeeder.CreateSkillAsync(context, name: "D");
            var skillE = await TestDataSeeder.CreateSkillAsync(context, name: "E");

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Linked in a deliberately scrambled order so the assertion proves the mapper sorts rather
            // than echoing insertion order. skillA and skillC share Order = 2 to exercise the SkillId
            // tie-break (which also covers legacy rows that all default to Order = 0).
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skillC.Id, selected: true, order: 2);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skillA.Id, selected: true, order: 2);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skillD.Id, selected: true, order: 0);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skillB.Id, selected: true, order: 1);
            // An unlocked-but-unselected skill must stay out of the equipped loadout.
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skillE.Id, selected: false, order: 0);

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // Equipped set is a stable total order by (Order, SkillId): D (0), B (1), then A and C
            // tie at 2 → A before C because its SkillId is lower.
            Assert.Equal(
                new[] { skillD.Id, skillB.Id, skillA.Id, skillC.Id },
                player.SelectedSkills.Select(skill => skill.Id));

            // The unselected skill is still unlocked but never equipped.
            Assert.Contains(player.Skills, skill => skill.Id == skillE.Id);
            Assert.DoesNotContain(player.SelectedSkills, skill => skill.Id == skillE.Id);
        }

        [Fact]
        public async Task SetFavorite_PersistsFavoriteFlagToDatabase()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var item = await TestDataSeeder.CreateItemAsync(context);
            context.UnlockedItems.Add(new Infrastructure.Entities.UnlockedItem
            {
                PlayerId = playerEntity.Id,
                ItemId = item.Id,
            });
            await context.SaveChangesAsync(CancellationToken);

            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();
            var player = await playerService.LoadPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var success = await playerService.SetFavorite(player, item.Id, favorite: true);
            Assert.True(success);

            // The write-behind queue is drained by the synchronizer; run it directly (the hosted
            // worker is disabled in the test harness) so the change reaches the database.
            await DrainPlayerUpdateQueue(scope.ServiceProvider);

            // Read the row from a fresh context (the cache-flush equivalent): the favorite flag must
            // have been persisted to the database, not just the cached domain player.
            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var persisted = await verifyContext.UnlockedItems
                .FirstOrDefaultAsync(ui => ui.PlayerId == playerEntity.Id && ui.ItemId == item.Id, CancellationToken);
            Assert.NotNull(persisted);
            Assert.True(persisted.Favorite);
        }

        private async Task DrainPlayerUpdateQueue(IServiceProvider services)
        {
            var pubsub = services.GetRequiredService<IPubSubService>();
            var synchronizer = new DataProviderSynchronizer(
                services, pubsub, NullLogger<DataProviderSynchronizer>.Instance, PlayerUpdateRetryPolicy.Default);
            await synchronizer.ProcessQueue(pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE));
        }

        private record SimpleAttributeUpdate(EAttribute Attribute, int Amount) : IAttributeUpdate;
    }
}
