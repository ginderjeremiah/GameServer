<svg
	width={size}
	height={size}
	viewBox="0 0 16 16"
	fill="none"
	style:stroke={color}
	stroke-width="1.2"
	stroke-linejoin="round"
	stroke-linecap="round"
	aria-hidden="true"
>
	<path d={PATHS[cat] ?? PATHS[EItemCategory.Accessory]} />
</svg>

<script lang="ts">
import { EItemCategory } from '$lib/api';

interface Props {
	cat: EItemCategory;
	color: string;
	size?: number;
}

const { cat, color, size = 22 }: Props = $props();

// Geometric stand-ins for the game's real item icons (item.iconPath).
const PATHS: Record<number, string> = {
	[EItemCategory.Helm]: 'M3.5 8.5C3.5 5 5.5 3 8 3s4.5 2 4.5 5.5v3h-9zM3.5 11.5h9M6 11.5v1.5M10 11.5v1.5',
	[EItemCategory.Chest]: 'M4 4l-1.5 2 1.5 1.5v4.5h8V7.5L13.5 6 12 4 9.5 5h-3zM6.5 4.5C6.5 5.5 7 6 8 6s1.5-.5 1.5-1.5',
	[EItemCategory.Leg]: 'M5 2.5h6l-.5 5L11 13H9l-1-4-1 4H5l.5-5.5z',
	[EItemCategory.Boot]: 'M5 2.5h3v6l4.5 2v2.5H5z M5 9h3',
	[EItemCategory.Weapon]: 'M11.5 2l2 .5.5 2-6 6-2.5-2.5zM5 8.5L2.5 12 4 13.5 7.5 11',
	[EItemCategory.Accessory]: 'M8 3.5l4.5 3.2-1.7 5.3H5.2L3.5 6.7zM8 7.2v0.01'
};
</script>
