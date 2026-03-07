<template>
  <span :class="['status-badge', badgeClass]">
    <span class="badge-dot" />
    {{ status || 'Stopped' }}
  </span>
</template>

<script setup lang="ts">
const props = defineProps<{
  status?: string
}>()

const badgeClass = computed(() => {
  const s = (props.status || 'Stopped').toLowerCase()
  if (s === 'running') return 'badge-success'
  if (s === 'crashed') return 'badge-danger'
  return 'badge-neutral'
})
</script>
