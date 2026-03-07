<template>
  <div class="content-view">
    <div class="content-header">
      <div>
        <h1 class="page-title">Processes</h1>
        <p class="page-subtitle">Manage your running commands and services</p>
      </div>
      <div class="header-actions">
        <button class="btn btn-primary" @click="$emit('create')">+ New Command</button>
      </div>
    </div>

    <div class="stats-row">
      <div class="stat-card">
        <span class="stat-label">Running</span>
        <span class="stat-value stat-success">{{ running }}</span>
      </div>
      <div class="stat-card">
        <span class="stat-label">Stopped</span>
        <span class="stat-value">{{ stopped }}</span>
      </div>
      <div class="stat-card">
        <span class="stat-label">Crashed</span>
        <span class="stat-value stat-danger">{{ crashed }}</span>
      </div>
      <div class="stat-card">
        <span class="stat-label">Total</span>
        <span class="stat-value">{{ processes.length }}</span>
      </div>
    </div>

    <div class="toolbar">
      <div class="search-box">
        <svg class="search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="11" cy="11" r="8" />
          <path d="m21 21-4.3-4.3" />
        </svg>
        <input
          v-model="search"
          type="text"
          class="search-input"
          placeholder="Search"
        />
      </div>
      <button class="btn btn-ghost" @click="$emit('refresh')">↻ Refresh</button>
    </div>

    <div class="table-container">
      <table class="data-table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Command</th>
            <th>Status</th>
            <th>Auto-Restart</th>
            <th style="text-align: right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="filtered.length === 0">
            <td colspan="5" class="empty-row">No commands found.</td>
          </tr>
          <tr v-for="p in filtered" :key="p.id">
            <td class="cell-name">{{ p.name }}</td>
            <td class="cell-command mono" :title="p.command">{{ p.command }}</td>
            <td><StatusBadge :status="p.status" /></td>
            <td>
              <span v-if="p.autoRestart" class="text-success" style="font-weight: 600">Yes</span>
              <span v-else class="text-muted">No</span>
            </td>
            <td class="cell-actions">
              <div class="action-group">
                <button class="btn-icon success" title="Start" @click="$emit('action', p.id, 'start')">▶</button>
                <button class="btn-icon danger" title="Stop" @click="$emit('action', p.id, 'stop')">■</button>
                <button class="btn-icon" title="Restart" @click="$emit('action', p.id, 'restart')">↻</button>
                <button class="btn-icon" title="Logs" @click="$emit('open-logs', p.id)">☰</button>
                <button class="btn-icon" title="Performance" @click="$emit('open-performance', p.id)">⚡</button>
                <button class="btn-icon" title="Edit" @click="$emit('edit', p)">✎</button>
                <button class="btn-icon danger" title="Delete" @click="$emit('delete', p.id)">✕</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Process } from '~/types'

const props = defineProps<{
  processes: Process[]
}>()

defineEmits<{
  create: []
  refresh: []
  edit: [process: Process]
  action: [id: string, action: string]
  delete: [id: string]
  'open-logs': [id: string]
  'open-performance': [id: string]
}>()

const search = ref('')

const running = computed(() => props.processes.filter((p) => p.status === 'Running').length)
const stopped = computed(() => props.processes.filter((p) => !p.status || p.status === 'Stopped').length)
const crashed = computed(() => props.processes.filter((p) => p.status === 'Crashed').length)

const filtered = computed(() => {
  if (!search.value) return props.processes
  const q = search.value.toLowerCase()
  return props.processes.filter(
    (p) => p.name?.toLowerCase().includes(q) || p.command?.toLowerCase().includes(q)
  )
})
</script>
