<template>
  <div v-if="!points.length" class="sparkline-empty">No CPU samples yet.</div>
  <svg
    v-else
    class="sparkline"
    viewBox="0 0 600 160"
    preserveAspectRatio="none"
  >
    <polyline
      fill="none"
      stroke="currentColor"
      stroke-width="2.5"
      :points="segments"
    />
  </svg>
</template>

<script setup lang="ts">
const props = defineProps<{
  points: number[]
}>()

const W = 600
const H = 160

const segments = computed(() => {
  if (!props.points.length) return ''
  const max = Math.max(1, ...props.points)
  return props.points
    .map((v, i) => {
      const x = (i / Math.max(props.points.length - 1, 1)) * W
      const y = H - (v / max) * H
      return `${x},${y}`
    })
    .join(' ')
})
</script>
