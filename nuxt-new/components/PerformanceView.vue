<template>
  <div class="content-view">
    <div class="content-header">
      <div>
        <h1 class="page-title">Performance</h1>
        <p class="page-subtitle">Monitor resource usage</p>
      </div>
    </div>

    <div class="perf-layout">
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

      <div class="perf-main">
        <div class="perf-grid">
          <div class="metric-card">
            <span class="metric-label">Status</span>
            <span class="metric-value">{{ selected?.status || '-' }}</span>
          </div>
          <div class="metric-card">
            <span class="metric-label">CPU</span>
            <span class="metric-value">{{ selected?.cpuPercent ?? '-' }}%</span>
          </div>
          <div class="metric-card">
            <span class="metric-label">PID</span>
            <span class="metric-value">{{ selected?.pid ?? '-' }}</span>
          </div>
          <div class="metric-card">
            <span class="metric-label">Working Set</span>
            <span class="metric-value">{{ selected?.workingSetDisplay || '-' }}</span>
          </div>
          <div class="metric-card">
            <span class="metric-label">Private Memory</span>
            <span class="metric-value">{{ selected?.privateMemoryDisplay || '-' }}</span>
          </div>
          <div class="metric-card">
            <span class="metric-label">Threads / Handles</span>
            <span class="metric-value">
              {{ selected ? `${selected.threadCount ?? '-'} / ${selected.handleCount ?? '-'}` : '-' }}
            </span>
          </div>
        </div>

        <div class="chart-container">
          <p class="chart-title">CPU trend (latest 60 samples)</p>
          <CpuSparkline :points="cpuPoints" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Process } from '~/types'

defineProps<{
  processes: Process[]
  selected: (Process & { workingSetDisplay: string; privateMemoryDisplay: string }) | null
  selectedId: string | null
  cpuPoints: number[]
}>()

defineEmits<{
  select: [id: string]
}>()
</script>
