export async function loadPanelConfig() {
  const response = await fetch("/panel-config.json", { cache: "no-store" });
  if (!response.ok) {
    throw new Error("Unable to load panel configuration.");
  }

  const json = await response.json();
  if (!json || typeof json.apiPort !== "number") {
    throw new Error("Panel configuration is invalid.");
  }

  return json;
}

async function parseError(response) {
  const text = await response.text();
  try {
    const json = JSON.parse(text);
    return json?.error || `Request failed (${response.status})`;
  } catch {
    return text || `Request failed (${response.status})`;
  }
}

export async function apiRequest(baseUrl, password, path, options = {}) {
  const request = {
    method: "GET",
    ...options,
    headers: {
      "X-CmdHub-Password": password || "",
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(options.headers || {})
    }
  };

  const response = await fetch(`${baseUrl}${path}`, request);
  if (!response.ok) {
    const message = await parseError(response);
    const error = new Error(message);
    error.status = response.status;
    throw error;
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}
