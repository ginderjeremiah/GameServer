<input bind:value type="checkbox" {disabled} />

<script lang="ts">
interface Props {
	value: boolean;
	disabled?: boolean;
}

let { value = $bindable(), disabled }: Props = $props();
</script>

<style lang="scss"></style>
