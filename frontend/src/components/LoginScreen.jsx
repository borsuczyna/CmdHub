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
      <div className="login-card">
        <div className="brand-icon" style={{ marginBottom: 16 }}>⌘</div>
        <h1>CmdHub</h1>
        <p className="subtitle">Sign in with your control panel password</p>

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label className="form-label" htmlFor="password">Password</label>
            <input
              id="password"
              name="password"
              type="password"
              className="form-input"
              autoComplete="current-password"
              placeholder="Enter password"
              required
            />
          </div>

          <div className="form-group">
            <label className="checkbox-label">
              <input name="remember" type="checkbox" />
              Remember on this browser
            </label>
          </div>

          {error ? <div className="error-box">{error}</div> : null}

          <button className="btn btn-primary" style={{ width: "100%" }} disabled={loading}>
            {loading ? "Signing In..." : "Sign In"}
          </button>
        </form>
      </div>
    </section>
  );
}
