import { useEffect, useMemo, useState } from "react";

const DEFAULT_MODEL = {
  name: "",
  command: "",
  workingDirectory: "",
  autoRestart: false,
  runOnStart: false,
  usePowerShell: false,
  runEveryEnabled: false,
  runEveryInterval: 5,
  runEveryUnit: "minutes",
  restartEveryEnabled: false,
  restartEveryInterval: 5,
  restartEveryUnit: "minutes"
};

export default function CommandDialog({ process, open, onClose, onSave }) {
  const model = useMemo(() => process || DEFAULT_MODEL, [process]);
  const [form, setForm] = useState(model);

  useEffect(() => {
    setForm(model);
  }, [model]);

  if (!open) {
    return null;
  }

  function update(key, value) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  function submit(event) {
    event.preventDefault();
    onSave(form);
  }

  return (
    <div className="dialog-backdrop" onClick={onClose}>
      <form className="dialog" onSubmit={submit} onClick={(e) => e.stopPropagation()}>
        <h4>{process ? "Edit Command" : "New Command"}</h4>

        <div className="form-group">
          <label className="form-label">Name</label>
          <input className="form-input" value={form.name} onChange={(e) => update("name", e.target.value)} required />
        </div>

        <div className="form-group">
          <label className="form-label">Command</label>
          <input className="form-input mono" value={form.command} onChange={(e) => update("command", e.target.value)} required />
        </div>

        <div className="form-group">
          <label className="form-label">Working Directory</label>
          <input className="form-input mono" value={form.workingDirectory} onChange={(e) => update("workingDirectory", e.target.value)} />
        </div>

        <div className="form-row">
          <div className="form-group">
            <label className="form-label">Run Every (min)</label>
            <input
              className="form-input"
              type="number"
              min="1"
              value={form.runEveryInterval}
              onChange={(e) => update("runEveryInterval", Number(e.target.value || 5))}
            />
          </div>
          <div className="form-group">
            <label className="form-label">Restart Every (min)</label>
            <input
              className="form-input"
              type="number"
              min="1"
              value={form.restartEveryInterval}
              onChange={(e) => update("restartEveryInterval", Number(e.target.value || 5))}
            />
          </div>
        </div>

        <div className="checkbox-grid">
          <label className="checkbox-label"><input type="checkbox" checked={form.autoRestart} onChange={(e) => update("autoRestart", e.target.checked)} /> Auto restart</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.runOnStart} onChange={(e) => update("runOnStart", e.target.checked)} /> Run on start</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.usePowerShell} onChange={(e) => update("usePowerShell", e.target.checked)} /> Use PowerShell</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.runEveryEnabled} onChange={(e) => update("runEveryEnabled", e.target.checked)} /> Enable run every</label>
          <label className="checkbox-label"><input type="checkbox" checked={form.restartEveryEnabled} onChange={(e) => update("restartEveryEnabled", e.target.checked)} /> Enable restart every</label>
        </div>

        <div className="dialog-actions">
          <button className="btn btn-ghost" type="button" onClick={onClose}>Cancel</button>
          <button className="btn btn-primary" type="submit">Save</button>
        </div>
      </form>
    </div>
  );
}
