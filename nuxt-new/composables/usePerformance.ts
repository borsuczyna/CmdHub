import { formatBytes } from '~/utils/formatBytes'
import type { Process } from '~/types'

export function usePerformance(processes: Ref<Process[]>) {
  const selectedPerfId = ref<string | null>(null)
  const cpuPoints = ref<number[]>([])

  const selectedPerf = computed(() => {
    const proc = processes.value.find((p) => p.id === selectedPerfId.value)
    if (!proc) return null
    return {
      ...proc,
      workingSetDisplay: formatBytes(proc.workingSetBytes),
      privateMemoryDisplay: formatBytes(proc.privateMemoryBytes)
    }
  })

  watch(
    () => selectedPerf.value?.cpuPercent,
    (val) => {
      if (val === undefined || val === null) return
      cpuPoints.value = [...cpuPoints.value, val].slice(-60)
    }
  )

  function selectProcess(id: string) {
    selectedPerfId.value = id
    cpuPoints.value = []
  }

  return { selectedPerfId, cpuPoints, selectedPerf, selectProcess }
}
