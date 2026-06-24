using Game.Abstractions;
using Game.Abstractions.Contracts.Admin;
using Game.Core;
using Game.DataAccess.Repositories.Admin;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    public class ReferenceFieldValidationTests
    {
        private sealed class TestModel : IModel
        {
            public required ERarity Rarity { get; init; }
        }

        private static Change<TestModel> Change(EChangeType changeType, ERarity rarity) => new()
        {
            ChangeType = changeType,
            Item = new TestModel { Rarity = rarity },
        };

        [Fact]
        public void FindUndefinedEnum_AllValid_ReturnsNull()
        {
            var changes = new[]
            {
                Change(EChangeType.Add, ERarity.Common),
                Change(EChangeType.Edit, ERarity.Mythic),
            };

            Assert.Null(ReferenceFieldValidation.FindUndefinedEnum(changes, m => m.Rarity, "rarity"));
        }

        [Fact]
        public void FindUndefinedEnum_UndefinedAdd_ReturnsFailureNamingTheValue()
        {
            // 0 is not a member of ERarity (Common = 1), so it has no backing reference row — the guard rejects
            // it up front instead of letting it 500 on the FK at commit.
            var result = ReferenceFieldValidation.FindUndefinedEnum(
                [Change(EChangeType.Add, (ERarity)0)], m => m.Rarity, "skill rarity");

            Assert.NotNull(result);
            Assert.False(result.Succeeded);
            Assert.Equal("0 is not a valid skill rarity.", result.ErrorMessage);
        }

        [Fact]
        public void FindUndefinedEnum_UndefinedEdit_IsRejected()
        {
            // Edits are validated too — a re-tier to an unmapped value is the same FK hazard as an insert.
            var result = ReferenceFieldValidation.FindUndefinedEnum(
                [Change(EChangeType.Edit, (ERarity)99)], m => m.Rarity, "rarity");

            Assert.NotNull(result);
            Assert.Equal("99 is not a valid rarity.", result.ErrorMessage);
        }

        [Fact]
        public void FindUndefinedEnum_UndefinedDelete_IsSkipped()
        {
            // A delete targets an existing row by id and carries no authored field worth validating, so an
            // unmapped value on a delete must not trip the guard.
            var result = ReferenceFieldValidation.FindUndefinedEnum(
                [Change(EChangeType.Delete, (ERarity)0)], m => m.Rarity, "rarity");

            Assert.Null(result);
        }

        [Fact]
        public void FindUndefinedEnum_ReturnsFirstViolationInOrder()
        {
            var changes = new[]
            {
                Change(EChangeType.Add, ERarity.Rare),
                Change(EChangeType.Add, (ERarity)7),
                Change(EChangeType.Edit, (ERarity)8),
            };

            var result = ReferenceFieldValidation.FindUndefinedEnum(changes, m => m.Rarity, "rarity");

            Assert.NotNull(result);
            Assert.Equal("7 is not a valid rarity.", result.ErrorMessage);
        }

        [Fact]
        public void FindUndefinedEnum_EmptyChangeSet_ReturnsNull()
        {
            Assert.Null(ReferenceFieldValidation.FindUndefinedEnum(
                Array.Empty<Change<TestModel>>(), m => m.Rarity, "rarity"));
        }
    }
}
