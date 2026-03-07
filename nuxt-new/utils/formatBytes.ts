export function formatBytes(bytes: unknown): string {
  if (typeof bytes !== 'number' || !Number.isFinite(bytes)) return '-'
  const kb = 1024
  const mb = 1024 * kb
  const gb = 1024 * mb
  if (bytes >= gb) return `${(bytes / gb).toFixed(2)} GB`
  if (bytes >= mb) return `${(bytes / mb).toFixed(2)} MB`
  if (bytes >= kb) return `${(bytes / kb).toFixed(2)} KB`
  return `${bytes} B`
}
