<template>
  <section class="login-screen">
    <div class="login-card">
      <div class="brand-icon" style="margin-bottom: 16px">⌘</div>
      <h1>CmdHub</h1>
      <p class="subtitle">Sign in with your control panel password</p>

      <form @submit.prevent="handleSubmit">
        <div class="form-group">
          <label class="form-label" for="password">Password</label>
          <input
            id="password"
            v-model="passwordInput"
            name="password"
            type="password"
            class="form-input"
            autocomplete="current-password"
            placeholder="Enter password"
            required
          />
        </div>

        <div class="form-group">
          <label class="checkbox-label">
            <input v-model="rememberMe" type="checkbox" />
            Remember on this browser
          </label>
        </div>

        <div v-if="error" class="error-box">{{ error }}</div>

        <button class="btn btn-primary" style="width: 100%" :disabled="loading">
          {{ loading ? 'Signing In...' : 'Sign In' }}
        </button>
      </form>
    </div>
  </section>
</template>

<script setup lang="ts">
const props = defineProps<{
  loading: boolean
  error: string
}>()

const emit = defineEmits<{
  login: [password: string, remember: boolean]
}>()

const passwordInput = ref('')
const rememberMe = ref(false)

function handleSubmit() {
  emit('login', passwordInput.value.trim(), rememberMe.value)
}
</script>
