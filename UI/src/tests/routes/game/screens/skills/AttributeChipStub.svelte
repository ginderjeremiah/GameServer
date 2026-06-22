<!-- Test stub for AttributeChip: a countable marker so SkillCardStats chip rendering can be asserted
     without pulling in the real icon/tooltip machinery. -->
<span data-testid="attr-chip" data-attr={attributeId}></span>

<script lang="ts">
const { attributeId }: { attributeId: number } = $props();
</script>
