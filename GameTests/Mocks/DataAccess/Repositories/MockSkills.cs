﻿using GameCore.DataAccess;
using GameCore.Entities.Skills;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockSkills : ISkills
    {
        public List<Skill> AllSkills()
        {
            throw new NotImplementedException();
        }

        public Skill GetSkill(int skillId)
        {
            throw new NotImplementedException();
        }

        public void SaveSkills(List<int> skillIds)
        {
            throw new NotImplementedException();
        }
    }
}
