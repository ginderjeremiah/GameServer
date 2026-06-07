using Game.Abstractions.DataAccess;
using Game.Application.Services;
using Game.Core;
using Game.Core.Attributes.Modifiers;
using Game.Core.Players;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
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
            context.ItemModSlots.Add(new Abstractions.Entities.ItemModSlot
            {
                ItemId = item.Id,
                ItemModSlotTypeId = (int)EItemModType.Prefix,
                Index = 0,
            });
            context.UnlockedItems.Add(new Abstractions.Entities.UnlockedItem
            {
                PlayerId = playerEntity.Id,
                ItemId = item.Id,
                EquipmentSlotId = (int)EEquipmentSlot.WeaponSlot,
            });

            // A Prefix mod granting Dexterity +7, unlocked for the player.
            var mod = await TestDataSeeder.CreateItemModAsync(context, attributeId: EAttribute.Dexterity, attributeAmount: 7m);
            context.UnlockedMods.Add(new Abstractions.Entities.UnlockedMod
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

        private record SimpleAttributeUpdate(EAttribute Attribute, int Amount) : IAttributeUpdate;
    }
}
