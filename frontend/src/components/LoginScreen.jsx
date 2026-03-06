export default function LoginScreen({ onLogin, error, loading }) {
  function handleSubmit(event) {
    event.preventDefault();
    const form = event.currentTarget;
    const password = form.password.value.trim();
    const remember = form.remember.checked;
    onLogin(password, remember);
  }

  return (
    <section className="login-screen">
      <div className="login-panel">
        <p className="eyebrow">CmdHub</p>
        <h1>Remote Control Panel</h1>
        <p className="subhead">
          Sign in with the control panel password from desktop settings.
        </p>

        <form onSubmit={handleSubmit} className="login-form">
          <label htmlFor="password">Password</label>
          <input
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            placeholder="Enter password"
            required
          />

          <label className="checkbox-line">
            <input name="remember" type="checkbox" />
            Remember on this browser
          </label>

          {error ? <p className="error-text">{error}</p> : null}

          <button className="primary" disabled={loading}>
            {loading ? "Signing In..." : "Sign In"}
          </button>
        </form>
      </div>
    </section>
  );
}
