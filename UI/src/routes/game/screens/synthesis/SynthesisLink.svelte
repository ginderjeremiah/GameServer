<!-- The shared "⟡ Synthesis" contextual nav link. Surfaced on the screens that connect to synthesis —
     the Skills loadout (synthesis feeds the loadout) and the Lexicon (proficiency mastery gates recipes) —
     so the markup + styling live once here; each parent positions it via its own layout. -->
<button type="button" class="synthesis-link" onclick={() => navigation.requestScreen('synthesis')}>
	⟡ Synthesis
</button>

<script lang="ts">
import { navigation } from '$stores';
</script>

<style lang="scss">
.synthesis-link {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	padding: 7px 13px;
	border: 1px solid color-mix(in srgb, var(--accent) 40%, var(--border-light));
	border-radius: 4px;
	background: color-mix(in srgb, var(--accent) 8%, transparent);
	color: var(--accent);
	cursor: pointer;

	&:hover {
		background: color-mix(in srgb, var(--accent) 16%, transparent);
	}
}
</style>
