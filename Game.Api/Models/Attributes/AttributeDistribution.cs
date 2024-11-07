﻿using Game.Core;
using AttributeDistributionEntity = Game.Core.Entities.AttributeDistribution;

namespace Game.Api.Models.Attributes
{
    public class AttributeDistribution : IModelFromSource<AttributeDistribution, AttributeDistributionEntity>
    {
        public EAttribute AttributeId { get; set; }
        public decimal BaseAmount { get; set; }
        public decimal AmountPerLevel { get; set; }

        public static AttributeDistribution FromSource(AttributeDistributionEntity entity)
        {
            return new AttributeDistribution
            {
                AttributeId = (EAttribute)entity.AttributeId,
                BaseAmount = entity.BaseAmount,
                AmountPerLevel = entity.AmountPerLevel,
            };
        }
    }
}