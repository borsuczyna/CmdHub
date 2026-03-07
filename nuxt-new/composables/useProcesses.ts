import { apiRequest } from '~/composables/useApi'
import type { Process, CommandForm } from '~/types'

export function useProcesses(apiBase: Ref<string>, password: Ref<string>) {
  const processes = ref<Process[]>([])

  async function refreshProcesses() {
    if (!apiBase.value) return
    const response = await apiRequest<{ processes: Process[] }>(apiBase.value, password.value, '/processes')
    processes.value = response?.processes || []
  }

  async function commandAction(id: string, action: string): Promise<void> {
    await apiRequest(apiBase.value, password.value, `/processes/${encodeURIComponent(id)}/actions/${action}`, { method: 'POST' })
    await refreshProcesses()
  }

  async function removeProcess(id: string): Promise<void> {
    await apiRequest(apiBase.value, password.value, `/processes/${encodeURIComponent(id)}`, { method: 'DELETE' })
    await refreshProcesses()
  }

  async function saveCommand(model: CommandForm, existingId?: string): Promise<void> {
    if (existingId) {
      await apiRequest(apiBase.value, password.value, `/processes/${encodeURIComponent(existingId)}`, {
        method: 'PUT',
        body: JSON.stringify(model)
      })
    } else {
      await apiRequest(apiBase.value, password.value, '/processes', {
        method: 'POST',
        body: JSON.stringify(model)
      })
    }
    await refreshProcesses()
  }

  return { processes, refreshProcesses, commandAction, removeProcess, saveCommand }
}
