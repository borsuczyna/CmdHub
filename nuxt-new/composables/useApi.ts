import type { PanelConfig } from '~/types'

export async function loadPanelConfig(): Promise<PanelConfig> {
  const response = await fetch('/panel-config.json', { cache: 'no-store' })
  if (!response.ok) {
    throw new Error('Unable to load panel configuration.')
  }
  const json = await response.json()
  if (!json || typeof json.apiPort !== 'number') {
    throw new Error('Panel configuration is invalid.')
  }
  return json as PanelConfig
}

async function parseError(response: Response): Promise<string> {
  const text = await response.text()
  try {
    const json = JSON.parse(text)
    return json?.error || `Request failed (${response.status})`
  } catch {
    return text || `Request failed (${response.status})`
  }
}

export async function apiRequest<T = unknown>(
  baseUrl: string,
  password: string,
  path: string,
  options: RequestInit = {}
): Promise<T | null> {
  const request: RequestInit = {
    method: 'GET',
    ...options,
    headers: {
      'X-CmdHub-Password': password || '',
      ...(options.body ? { 'Content-Type': 'application/json' } : {}),
      ...(options.headers || {})
    }
  }
  const response = await fetch(`${baseUrl}${path}`, request)
  if (!response.ok) {
    const message = await parseError(response)
    const error = Object.assign(new Error(message), { status: response.status })
    throw error
  }
  if (response.status === 204) return null
  return response.json() as Promise<T>
}
