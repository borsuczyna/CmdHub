const tabs = [
  { id: "processes", label: "Processes" },
  { id: "logs", label: "Logs" },
  { id: "performance", label: "Performance" }
];

export default function Tabs({ value, onChange }) {
  return (
    <nav className="tabs">
      {tabs.map((tab) => (
        <button
          key={tab.id}
          className={value === tab.id ? "tab active" : "tab"}
          onClick={() => onChange(tab.id)}
          type="button"
        >
          {tab.label}
        </button>
      ))}
    </nav>
  );
}
