<template>
  <button
    class="mobile-menu-btn"
    type="button"
    aria-label="Open menu"
    @click="mobileOpen = true"
  >
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
      <path d="M3 12h18M3 6h18M3 18h18" />
    </svg>
  </button>

  <div v-if="mobileOpen" class="sidebar-overlay" @click="mobileOpen = false" />

  <aside :class="['sidebar', { open: mobileOpen }]">
    <div class="sidebar-brand">
      <div class="brand-icon">⌘</div>
      <span class="brand-name">CmdHub</span>
      <button
        class="mobile-close-btn"
        type="button"
        aria-label="Close menu"
        @click="mobileOpen = false"
      >
        ✕
      </button>
    </div>

    <nav class="sidebar-nav">
      <div class="sidebar-section">Navigation</div>
      <button
        v-for="item in navItems"
        :key="item.id"
        :class="['nav-item', { active: activeTab === item.id }]"
        type="button"
        @click="navigate(item.id)"
      >
        <span class="nav-icon">{{ item.icon }}</span>
        {{ item.label }}
      </button>
    </nav>

    <div class="sidebar-footer">
      <div class="sidebar-info">
        API: {{ apiPort || '-' }} · Panel: {{ panelPort || '-' }}
      </div>
      <button class="nav-item" type="button" @click="onSettingsClick">
        <span class="nav-icon">⚙</span>
        Settings
      </button>
      <button class="nav-item" type="button" @click="onLogoutClick">
        <span class="nav-icon">↗</span>
        Sign Out
      </button>
    </div>
  </aside>
</template>

<script setup lang="ts">
import type { TabId } from '~/types'

const NAV_ITEMS = [
  { id: 'processes' as TabId, label: 'Processes', icon: '⊞' },
  { id: 'logs' as TabId, label: 'Logs', icon: '☰' },
  { id: 'performance' as TabId, label: 'Performance', icon: '⚡' }
]

const props = defineProps<{
  activeTab: TabId
  apiPort: number | null
  panelPort: number | null
}>()

const emit = defineEmits<{
  'tab-change': [id: TabId]
  settings: []
  logout: []
}>()

const navItems = NAV_ITEMS
const mobileOpen = ref(false)

function navigate(id: TabId) {
  emit('tab-change', id)
  mobileOpen.value = false
}

function onSettingsClick() {
  emit('settings')
  mobileOpen.value = false
}

function onLogoutClick() {
  emit('logout')
  mobileOpen.value = false
}
</script>
