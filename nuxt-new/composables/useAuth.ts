import { loadPanelConfig, apiRequest } from '~/composables/useApi'

const PASSWORD_KEY = 'cmdhub-panel-password'

export function useAuth() {
  const apiPort = ref<number | null>(null)
  const panelPort = ref<number | null>(null)
  const password = ref('')
  const authed = ref(false)
  const authError = ref('')
  const authBusy = ref(false)

  const apiBase = computed(() => {
    if (!apiPort.value) return ''
    return `${window.location.protocol}//${window.location.hostname}:${apiPort.value}/api`
  })

  async function init() {
    try {
      const config = await loadPanelConfig()
      apiPort.value = config.apiPort
      panelPort.value = config.panelPort ?? null
    } catch (error: unknown) {
      authError.value = (error as Error).message
    }
  }

  async function login(value: string, remember: boolean) {
    if (!apiBase.value) return
    authBusy.value = true
    authError.value = ''
    try {
      await apiRequest(apiBase.value, value, '/processes')
      password.value = value
      authed.value = true
      if (remember) {
        localStorage.setItem(PASSWORD_KEY, value)
      } else {
        localStorage.removeItem(PASSWORD_KEY)
      }
    } catch (error: unknown) {
      authed.value = false
      authError.value = (error as Error).message || 'Authentication failed.'
    } finally {
      authBusy.value = false
    }
  }

  function logout() {
    authed.value = false
    password.value = ''
    localStorage.removeItem(PASSWORD_KEY)
  }

  function tryRemembered() {
    const remembered = localStorage.getItem(PASSWORD_KEY)
    if (remembered) {
      login(remembered, true)
    }
  }

  return { apiPort, panelPort, password, authed, authError, authBusy, apiBase, init, login, logout, tryRemembered }
}
