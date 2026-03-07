export interface Process {
  id: string
  name: string
  command: string
  workingDirectory?: string
  status: string
  cpuPercent?: number
  workingSetBytes?: number
  privateMemoryBytes?: number
  pid?: number
  threadCount?: number
  handleCount?: number
  autoRestart: boolean
  runOnStart?: boolean
  usePowerShell?: boolean
  runEveryEnabled?: boolean
  runEveryInterval?: number
  runEveryUnit?: string
  restartEveryEnabled?: boolean
  restartEveryInterval?: number
  restartEveryUnit?: string
}

export interface PanelConfig {
  apiPort: number
  panelPort?: number
}

export interface CommandForm {
  name: string
  command: string
  workingDirectory: string
  autoRestart: boolean
  runOnStart: boolean
  usePowerShell: boolean
  runEveryEnabled: boolean
  runEveryInterval: number
  runEveryUnit: string
  restartEveryEnabled: boolean
  restartEveryInterval: number
  restartEveryUnit: string
}

export type TabId = 'processes' | 'logs' | 'performance'
