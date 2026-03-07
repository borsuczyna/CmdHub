<template>
  <div class="content-view">
    <div class="content-header">
      <div>
        <h1 class="page-title">Logs</h1>
        <p class="page-subtitle">View console output from your processes</p>
      </div>
      <div class="header-actions">
        <input
          class="form-input"
          style="width: 100px"
          type="number"
          min="100"
          max="200000"
          :value="tail"
          @change="$emit('tail-change', Number(($event.target as HTMLInputElement).value || 16000))"
        />
        <label class="checkbox-label">
          <input
            type="checkbox"
            :checked="autoRefresh"
            @change="$emit('auto-refresh-change', ($event.target as HTMLInputElement).checked)"
          />
          Auto refresh
        </label>
        <button class="btn btn-ghost" @click="$emit('refresh')">Refresh</button>
        <button class="btn btn-danger" :disabled="!selectedId" @click="$emit('clear')">Clear</button>
      </div>
    </div>

    <div class="logs-layout">
      <aside class="process-sidebar">
        <button
          v-for="p in processes"
          :key="p.id"
          :class="['process-sidebar-item', { active: selectedId === p.id }]"
          @click="$emit('select', p.id)"
        >
          <strong>{{ p.name }}</strong>
          <span>{{ p.status }}</span>
        </button>
      </aside>

      <div class="log-panel">
        <div class="log-panel-header">
          <h4>{{ selected ? selected.name : 'No process selected' }}</h4>
          <p>{{ selected ? selected.command : 'Choose a process from the sidebar.' }}</p>
        </div>
        <pre class="log-body">{{ logs || 'No logs yet.' }}</pre>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Process } from '~/types'

const props = defineProps<{
  processes: Process[]
  selectedId: string | null
  logs: string
  tail: number
  autoRefresh: boolean
}>()

defineEmits<{
  select: [id: string]
  'tail-change': [value: number]
  'auto-refresh-change': [value: boolean]
  refresh: []
  clear: []
}>()

const selected = computed(() => props.processes.find((p) => p.id === props.selectedId) || null)
</script>
