# CmdHub

CmdHub is a Windows WPF app for running and monitoring multiple command-line processes from one place.

It is designed for workflows where you need several long-running commands (APIs, workers, scripts, watchers) and want simple start/stop/restart controls with output previews.

## Features

- Manage multiple commands in one UI
- Per-command actions: Start, Stop, Restart, Open Console, Edit, Delete
- Per-command actions: Start, Stop, Restart, Open Console, Perf, Edit, Delete
- Live output preview in a dedicated console window
- Optional local HTTP API (enable/disable from toolbar)
- Remote HTML control panel (on separate port)
- Console action to send `Ctrl+C` for graceful interruption
- Auto-restart support for crashed/exited processes
- Run-on-start support
- Minimize to system tray with tray context menu
- Persisted command configuration in `%APPDATA%\CmdHub\config.json`

## Screenshots
<img width="1046" height="656" alt="image" src="https://github.com/user-attachments/assets/dd04617b-bbe5-4481-b822-4660a02c96d6" />
<img width="869" height="545" alt="image" src="https://github.com/user-attachments/assets/8400be66-31da-4163-abef-2c690c6312be" />
<img width="560" height="435" alt="image" src="https://github.com/user-attachments/assets/6e3d7576-0226-477a-962b-2936b535bdff" />

## Requirements

- Windows
- .NET SDK 8.0+

## Build And Run

From repository root:

```powershell
dotnet build .\CmdHub.sln
dotnet run --project .\CmdHub.csproj
```

## Usage

1. Click `New Command`.
2. Fill in:
	 - `Name`: friendly display name
	 - `Command`: full command to execute (for example `dotnet run --project MyApi.csproj`)
	 - `Working Directory`: optional start directory
	 - `Auto Restart`: restart after unexpected exit
	 - `Run On Start`: start automatically when CmdHub launches
	 - `Use PowerShell`: run command via `powershell.exe` (useful for shell aliases/functions)
3. Use row buttons to control each process.
4. Click `Console` to inspect full output.
5. Click `Remote: Off` in toolbar to enable remote API + control panel.
6. Click `Settings` to configure API port, control panel port, and control panel password.

## API Endpoints

When enabled, API base URL is:

`http://localhost:5480/api`

- `GET /api/health`
- `GET /api/processes`
- `GET /api/processes/{processId}/logs`
- `POST /api/processes` (create command)
- `PUT /api/processes/{processId}` (edit command)
- `DELETE /api/processes/{processId}` (delete command)
- `POST /api/processes/{processId}/actions/start`
- `POST /api/processes/{processId}/actions/stop`
- `POST /api/processes/{processId}/actions/restart`
- `POST /api/processes/{processId}/actions/ctrlc`
- `POST /api/processes/{processId}/actions/clear-logs`

`logs` response contains `logs` as a string array (one item per log line).

All endpoints (except `/api/health`) require header:

- `X-CmdHub-Password: <control-panel-password>`

## Control Panel

Control panel URL (default):

`http://<machine-ip>:5481/`

The HTML panel can be opened from another device in the same network and supports:

- Process list with status/usage
- Start / Stop / Restart / Ctrl+C
- Create / Edit / Delete command
- Per-process logs view

`logs` endpoint query options:

- `tail` (optional, number of chars returned from end of log, default `16000`, max `200000`)

## Configuration

CmdHub stores config at:

`%APPDATA%\CmdHub\config.json`

Config shape:

```json
{
	"apiEnabled": false,
	"apiPort": 5480,
	"controlPanelPort": 5481,
	"controlPanelPassword": "generated-random-password",
	"commands": [
		{
			"id": "guid",
			"name": "My API",
			"command": "dotnet run --project MyApi.csproj",
			"workingDirectory": "C:\\path\\to\\project",
			"autoRestart": true,
			"runOnStart": false,
			"usePowerShell": false
		}
	]
}
```

## Process Behavior

- `Stop` attempts to terminate the full process tree.
- `Ctrl+C` in console sends an interrupt through standard input (best effort graceful stop).
- App exit also triggers cleanup for all managed processes.
- If a process ignores normal termination, CmdHub uses a forced tree-kill fallback on Windows.

## Troubleshooting

- Build fails with file lock (`CmdHub.exe is being used by another process`):

```powershell
Get-Process CmdHub -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build .\CmdHub.sln
```

- Command does not start:
	- Verify executable exists in `PATH` or provide full path.
	- Verify `Working Directory` is valid.
	- For shell-specific commands/aliases, enable `Use PowerShell`.
	- Open `Console` for error output.

## Project Structure

- `MainWindow.xaml` / `MainWindow.xaml.cs`: main grid UI + tray behavior
- `ViewModels/MainViewModel.cs`: command list orchestration
- `ViewModels/CommandViewModel.cs`: process lifecycle and output buffering
- `Views/ConsoleWindow.xaml` / `Views/ConsoleWindow.xaml.cs`: log viewer window
- `Services/ConfigService.cs`: load/save JSON config
- `Models/`: config models

## License

MIT License. See [LICENSE](LICENSE) for details.

