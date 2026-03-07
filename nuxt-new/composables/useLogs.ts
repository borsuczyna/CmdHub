import { apiRequest } from '~/composables/useApi'

export function useLogs(apiBase: Ref<string>, password: Ref<string>) {
  const logsText = ref('')
  const logsTail = ref(16000)
  const logsAutoRefresh = ref(true)
  const selectedLogsId = ref<string | null>(null)

  async function refreshLogs() {
    if (!selectedLogsId.value || !apiBase.value) return
    const response = await apiRequest<{ logs: string[] }>(
      apiBase.value,
      password.value,
      `/processes/${encodeURIComponent(selectedLogsId.value)}/logs?tail=${logsTail.value}`
    )
    logsText.value = (response?.logs || []).join('\n')
  }

  async function clearLogs() {
    if (!selectedLogsId.value || !apiBase.value) return
    await apiRequest(apiBase.value, password.value, `/processes/${encodeURIComponent(selectedLogsId.value)}/actions/clear-logs`, { method: 'POST' })
    logsText.value = ''
  }

  function selectProcess(id: string) {
    selectedLogsId.value = id
    logsText.value = ''
  }

  return { logsText, logsTail, logsAutoRefresh, selectedLogsId, refreshLogs, clearLogs, selectProcess }
}
