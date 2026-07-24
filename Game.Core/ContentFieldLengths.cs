namespace Game.Core
{
    /// <summary>
    /// The EF <c>HasMaxLength</c> bound for every authored text/textarea column the admin Workbench
    /// edits, named per entity+property (<c>GameContext</c> configures each column against the matching
    /// constant here). Marked <see cref="ClientMirroredAttribute"/> so the code generator mirrors each
    /// value into <c>game-constants.ts</c>, giving the Workbench field configs (<c>workbench/entities/*.ts</c>)
    /// a single source of truth to assert against instead of a hand-copied literal that can silently drift
    /// from the DB bound (issue #2318; the convention itself is #2257).
    /// </summary>
    [ClientMirrored]
    public static class ContentFieldLengths
    {
        public const int ChallengeNameMaxLength = 100;
        public const int ChallengeDescriptionMaxLength = 500;
        public const int ChallengeDesignerNotesMaxLength = 2000;

        public const int ClassNameMaxLength = 50;
        public const int ClassWordMaxLength = 50;
        public const int ClassDescriptionMaxLength = 500;
        public const int ClassDesignerNotesMaxLength = 2000;

        public const int EnemyNameMaxLength = 50;
        public const int EnemyDesignerNotesMaxLength = 2000;

        public const int ItemNameMaxLength = 50;
        public const int ItemIconPathMaxLength = 50;
        public const int ItemDescriptionMaxLength = 500;
        public const int ItemDesignerNotesMaxLength = 2000;

        public const int ItemModNameMaxLength = 50;
        public const int ItemModDescriptionMaxLength = 500;
        public const int ItemModDesignerNotesMaxLength = 2000;

        public const int SkillNameMaxLength = 50;
        public const int SkillIconPathMaxLength = 50;
        public const int SkillWordMaxLength = 50;
        public const int SkillPronunciationMaxLength = 50;
        public const int SkillTranslationMaxLength = 100;
        public const int SkillDescriptionMaxLength = 500;
        public const int SkillDesignerNotesMaxLength = 2000;

        public const int SkillRecipeDesignerNotesMaxLength = 2000;

        public const int LessonKeyMaxLength = 100;
        public const int LessonNameMaxLength = 100;
        public const int LessonScreenKeyMaxLength = 50;
        public const int LessonDesignerNotesMaxLength = 2000;

        public const int LessonStepTextMaxLength = 500;
        public const int LessonStepAnchorKeyMaxLength = 100;

        public const int PathNameMaxLength = 50;
        public const int PathDescriptionMaxLength = 500;
        public const int PathDesignerNotesMaxLength = 2000;

        public const int ProficiencyNameMaxLength = 50;
        public const int ProficiencyDescriptionMaxLength = 500;
        public const int ProficiencyIconPathMaxLength = 50;
        public const int ProficiencyWordMaxLength = 50;
        public const int ProficiencyPronunciationMaxLength = 50;
        public const int ProficiencyTranslationMaxLength = 100;
        public const int ProficiencyDesignerNotesMaxLength = 2000;

        public const int TagNameMaxLength = 50;

        public const int ZoneNameMaxLength = 50;
        public const int ZoneDescriptionMaxLength = 500;
        public const int ZoneDesignerNotesMaxLength = 2000;
    }
}
