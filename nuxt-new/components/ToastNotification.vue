<template>
  <div v-if="message" class="toast">{{ message }}</div>
</template>

<script setup lang="ts">
defineProps<{
  message: string
}>()
</script>
