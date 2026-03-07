<template>
  <div>
    <LoginScreen
      v-if="!authed"
      :loading="authBusy"
      :error="authError"
      @login="onLogin"
    />

    <div v-else class="app-layout">
      <AppSidebar
        :active-tab="activeTab"
        :api-port="apiPort"
        :panel-port="panelPort"
        @tab-change="activeTab = $event"
        @settings="toast = 'Settings are managed in the desktop app.'"
        @logout="onLogout"
      />

      <main class="main-content">
        <ProcessesView
          v-if="activeTab === 'processes'"
          :processes="processes"
          @create="openCreateDialog"
          @refresh="refreshProcesses"
          @edit="openEditDialog"
          @action="onAction"
          @delete="onDelete"
          @open-logs="onOpenLogs"
          @open-performance="onOpenPerformance"
        />

        <LogsView
          v-if="activeTab === 'logs'"
          :processes="processes"
          :selected-id="selectedLogsId"
          :logs="logsText"
          :tail="logsTail"
          :auto-refresh="logsAutoRefresh"
          @select="selectLogsProcess"
          @tail-change="logsTail = $event"
          @auto-refresh-change="logsAutoRefresh = $event"
          @refresh="refreshLogs"
          @clear="clearLogs"
        />

        <PerformanceView
          v-if="activeTab === 'performance'"
          :processes="processes"
          :selected="selectedPerf"
          :selected-id="selectedPerfId"
          :cpu-points="cpuPoints"
          @select="selectPerfProcess"
        />

        <CommandDialog
          :process="dialogProcess"
          :open="dialogOpen"
          @close="closeDialog"
          @save="onSave"
        />

        <ToastNotification :message="toast" />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Process, CommandForm, TabId } from '~/types'

const { apiPort, panelPort, password, authed, authError, authBusy, apiBase, init, login, logout, tryRemembered } = useAuth()
const { processes, refreshProcesses, commandAction, removeProcess, saveCommand } = useProcesses(apiBase, password)
const { logsText, logsTail, logsAutoRefresh, selectedLogsId, refreshLogs, clearLogs, selectProcess: selectLogsProcess } = useLogs(apiBase, password)
const { selectedPerfId, cpuPoints, selectedPerf, selectProcess: selectPerfProcess } = usePerformance(processes)

const activeTab = ref<TabId>('processes')
const dialogOpen = ref(false)
const dialogProcess = ref<Process | null>(null)
const toast = ref('')

let processTimer: ReturnType<typeof setInterval> | null = null
let logsTimer: ReturnType<typeof setInterval> | null = null

onMounted(async () => {
  await init()
  tryRemembered()
})

watch(authed, (val) => {
  if (val && apiBase.value) {
    startPolling()
  } else {
    stopPolling()
  }
})

watch([activeTab, selectedLogsId, logsTail, logsAutoRefresh], () => {
  if (logsTimer) clearInterval(logsTimer)
  if (authed.value && activeTab.value === 'logs' && selectedLogsId.value && logsAutoRefresh.value) {
    refreshLogs()
    logsTimer = setInterval(refreshLogs, 1600)
  }
})

function startPolling() {
  refreshProcesses()
  processTimer = setInterval(refreshProcesses, 2000)
}

function stopPolling() {
  if (processTimer) clearInterval(processTimer)
  if (logsTimer) clearInterval(logsTimer)
}

watch(processes, (list) => {
  if (!selectedLogsId.value && list.length) selectedLogsId.value = list[0].id
  if (!selectedPerfId.value && list.length) selectPerfProcess(list[0].id)
})

watch(toast, (val) => {
  if (!val) return
  setTimeout(() => { toast.value = '' }, 2200)
})

async function onLogin(value: string, remember: boolean) {
  await login(value, remember)
}

function onLogout() {
  stopPolling()
  logout()
}

async function onAction(id: string, action: string) {
  await commandAction(id, action)
  toast.value = `Action ${action} requested.`
}

async function onDelete(id: string) {
  if (!confirm('Delete this command?')) return
  await removeProcess(id)
  toast.value = 'Command deleted.'
}

function openCreateDialog() {
  dialogProcess.value = null
  dialogOpen.value = true
}

function openEditDialog(process: Process) {
  dialogProcess.value = process
  dialogOpen.value = true
}

function closeDialog() {
  dialogOpen.value = false
  dialogProcess.value = null
}

async function onSave(model: CommandForm) {
  await saveCommand(model, dialogProcess.value?.id)
  toast.value = dialogProcess.value ? 'Command updated.' : 'Command created.'
  closeDialog()
}

function onOpenLogs(id: string) {
  selectLogsProcess(id)
  activeTab.value = 'logs'
}

function onOpenPerformance(id: string) {
  selectPerfProcess(id)
  activeTab.value = 'performance'
}

onUnmounted(stopPolling)
</script>
