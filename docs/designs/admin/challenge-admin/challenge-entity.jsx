// ─────────────────────────────────────────────────────────────────────────
//  CHALLENGE entity config. Plugs into the same Workbench shell as Enemies,
//  Skills, Items, etc. Identity is a plain fields section; Condition and Reward
//  are custom sections (rendered by ChallengeSection) because they carry the
//  conditional safety logic the generic kinds don't cover.
// ─────────────────────────────────────────────────────────────────────────
const CHALLENGE_ENTITY = {
	key: 'challenges', label: 'Challenges', singular: 'Challenge', glyph: 'trophy',
	seed: window.CHALLENGES,
	blankName: 'Untitled challenge',
	newItem: (id) => window.makeChallenge(id, { name: '', desc: '', type: 1, target: null, goal: 10, item: null, mod: null }),
	listBadge: (c) => window.typeName(c.type),
	badgeColor: () => 'var(--accent)',
	meta: (c) => {
		const r = (c.rewardItemId != null ? 1 : 0) + (c.rewardItemModId != null ? 1 : 0);
		return [['goal', window.fmtNum(c.progressGoal)], ['', c.targetEntityId != null ? 'scoped' : 'global'], ['reward', r]];
	},
	sections: [
		{
			key: 'identity', label: 'Identity', glyph: 'tag', desc: 'Name & player-facing description',
			complete: (c) => !!(c.name && c.name.trim()), detail: (c) => (c.name ? `“${c.name}”` : 'Name required'),
			kind: 'fields', fields: [
				{ key: 'name', label: 'Challenge Name', type: 'text', placeholder: 'Name this challenge…', grow: true, required: true, reqMsg: 'Missing name' },
				{ key: 'description', label: 'Description', type: 'textarea', placeholder: 'Describe the objective shown to players…', grow: true, required: true, reqMsg: 'No description' }
			]
		},
		{
			key: 'condition', label: 'Condition', glyph: 'target', desc: 'What the player must do to complete it',
			kind: 'challenge-condition',
			// Statistic & entity are now fully determined by the type, so they can't
			// be inconsistent. The only condition-level slip is a non-positive goal.
			warn: (c) => (!c.progressGoal || c.progressGoal <= 0) ? 'Goal must be greater than zero' : null
		},
		{
			key: 'reward', label: 'Reward', glyph: 'gift', desc: 'What the player unlocks on completion',
			count: (c) => (c.rewardItemId != null ? 1 : 0) + (c.rewardItemModId != null ? 1 : 0),
			kind: 'challenge-reward',
			warn: (c) => (c.rewardItemId == null && c.rewardItemModId == null) ? 'No reward — unlocks nothing' : null
		}
	]
};

window.ENTITIES = Object.assign({}, window.ENTITIES, { challenges: CHALLENGE_ENTITY });
window.CHALLENGE_ENTITY = CHALLENGE_ENTITY;
