<svg
	width={size}
	height={size}
	viewBox="0 0 16 16"
	fill="none"
	stroke={color}
	stroke-width={strokeWidth}
	stroke-linecap="round"
	stroke-linejoin="round"
	aria-hidden="true"
>
	{#if typeId === EChallengeType.TimeTrial}
		<circle cx="8" cy="9.2" r="4.3" />
		<path d="M6.4 2.6h3.2M8 2.6v1.9M8 9.2V6.6M8 9.2l2.1 1.5" />
	{:else}
		<path d={GLYPH_PATH[typeId]} />
	{/if}
</svg>

<script lang="ts">
import { EChallengeType } from '$lib/api';

interface Props {
	typeId: EChallengeType;
	color: string;
	size?: number;
	strokeWidth?: number;
}

const { typeId, color, size = 16, strokeWidth = 1.3 }: Props = $props();

// Abstract line glyphs, one per challenge type (TimeTrial draws a clock above).
const GLYPH_PATH: Record<number, string> = {
	[EChallengeType.EnemiesKilled]: 'M3.5 3.5l9 9M12.5 3.5l-9 9',
	[EChallengeType.BossesDefeated]: 'M2.5 11.5h11M3.5 11.5l.6-5.5 2.4 3L8 5l1.5 4 2.4-3 .6 5.5',
	[EChallengeType.ZonesCleared]: 'M4 2.5v11M4 3.5h7l-1.8 2.2L11 8H4',
	[EChallengeType.LevelReached]: 'M3.5 8.5l4.5-4 4.5 4M3.5 12.5l4.5-4 4.5 4',
	[EChallengeType.DamageDealt]:
		'M8 2.2v2.4M8 11.4v2.4M2.2 8h2.4M11.4 8h2.4M4.2 4.2l1.7 1.7M10.1 10.1l1.7 1.7M11.8 4.2L10.1 5.9M5.9 10.1l-1.7 1.7',
	[EChallengeType.BattlesWon]: 'M8 2.4l5 1.8v3.6c0 3-2.4 5-5 5.8-2.6-.8-5-2.8-5-5.8V4.2zM5.8 7.9l1.7 1.7L10.4 6',
	[EChallengeType.SkillsUsed]: 'M8 1.8l1.6 4.6 4.6 1.6-4.6 1.6L8 14.2l-1.6-4.6L1.8 8l4.6-1.6z'
};
</script>
