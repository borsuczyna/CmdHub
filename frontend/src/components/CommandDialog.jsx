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
      <form className="dialog" onSubmit={submit} onClick={(event) => event.stopPropagation()}>
        <h4>{process ? "Edit Command" : "New Command"}</h4>

        <label>Name</label>
        <input value={form.name} onChange={(event) => update("name", event.target.value)} required />

        <label>Command</label>
        <input className="mono" value={form.command} onChange={(event) => update("command", event.target.value)} required />

        <label>Working Directory</label>
        <input className="mono" value={form.workingDirectory} onChange={(event) => update("workingDirectory", event.target.value)} />

        <div className="mini-grid">
          <div>
            <span>Run Every</span>
            <input
              type="number"
              min="1"
              value={form.runEveryInterval}
              onChange={(event) => update("runEveryInterval", Number(event.target.value || 5))}
            />
          </div>
          <div>
            <span>Restart Every</span>
            <input
              type="number"
              min="1"
              value={form.restartEveryInterval}
              onChange={(event) => update("restartEveryInterval", Number(event.target.value || 5))}
            />
          </div>
        </div>

        <div className="checkbox-grid">
          <label className="checkbox-line"><input type="checkbox" checked={form.autoRestart} onChange={(event) => update("autoRestart", event.target.checked)} />Auto restart</label>
          <label className="checkbox-line"><input type="checkbox" checked={form.runOnStart} onChange={(event) => update("runOnStart", event.target.checked)} />Run on start</label>
          <label className="checkbox-line"><input type="checkbox" checked={form.usePowerShell} onChange={(event) => update("usePowerShell", event.target.checked)} />Use PowerShell</label>
          <label className="checkbox-line"><input type="checkbox" checked={form.runEveryEnabled} onChange={(event) => update("runEveryEnabled", event.target.checked)} />Enable run every</label>
          <label className="checkbox-line"><input type="checkbox" checked={form.restartEveryEnabled} onChange={(event) => update("restartEveryEnabled", event.target.checked)} />Enable restart every</label>
        </div>

        <div className="row-actions">
          <button className="ghost" type="button" onClick={onClose}>Cancel</button>
          <button className="primary" type="submit">Save</button>
        </div>
      </form>
    </div>
  );
}
