<template>
  <div v-if="open" class="dialog-backdrop" @click="$emit('close')">
    <form class="dialog" @submit.prevent="submit" @click.stop>
      <h4>{{ process ? 'Edit Command' : 'New Command' }}</h4>

      <div class="form-group">
        <label class="form-label">Name</label>
        <input v-model="form.name" class="form-input" required />
      </div>

      <div class="form-group">
        <label class="form-label">Command</label>
        <input v-model="form.command" class="form-input mono" required />
      </div>

      <div class="form-group">
        <label class="form-label">Working Directory</label>
        <input v-model="form.workingDirectory" class="form-input mono" />
      </div>

      <div class="form-row">
        <div class="form-group">
          <label class="form-label">Run Every (min)</label>
          <input v-model.number="form.runEveryInterval" class="form-input" type="number" min="1" />
        </div>
        <div class="form-group">
          <label class="form-label">Restart Every (min)</label>
          <input v-model.number="form.restartEveryInterval" class="form-input" type="number" min="1" />
        </div>
      </div>

      <div class="checkbox-grid">
        <label class="checkbox-label"><input v-model="form.autoRestart" type="checkbox" /> Auto restart</label>
        <label class="checkbox-label"><input v-model="form.runOnStart" type="checkbox" /> Run on start</label>
        <label class="checkbox-label"><input v-model="form.usePowerShell" type="checkbox" /> Use PowerShell</label>
        <label class="checkbox-label"><input v-model="form.runEveryEnabled" type="checkbox" /> Enable run every</label>
        <label class="checkbox-label"><input v-model="form.restartEveryEnabled" type="checkbox" /> Enable restart every</label>
      </div>

      <div class="dialog-actions">
        <button class="btn btn-ghost" type="button" @click="$emit('close')">Cancel</button>
        <button class="btn btn-primary" type="submit">Save</button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import type { Process, CommandForm } from '~/types'

const DEFAULT_FORM: CommandForm = {
  name: '',
  command: '',
  workingDirectory: '',
  autoRestart: false,
  runOnStart: false,
  usePowerShell: false,
  runEveryEnabled: false,
  runEveryInterval: 5,
  runEveryUnit: 'minutes',
  restartEveryEnabled: false,
  restartEveryInterval: 5,
  restartEveryUnit: 'minutes'
}

const props = defineProps<{
  process: Process | null
  open: boolean
}>()

const emit = defineEmits<{
  close: []
  save: [form: CommandForm]
}>()

const form = ref<CommandForm>({ ...DEFAULT_FORM })

watch(
  () => props.process,
  (val) => {
    form.value = val ? { ...DEFAULT_FORM, ...val } : { ...DEFAULT_FORM }
  },
  { immediate: true }
)

function submit() {
  emit('save', { ...form.value })
}
</script>
