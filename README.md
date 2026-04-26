# tia-portal-mcp

MCP server for Siemens SIMATIC TIA Portal V21. It lets MCP clients and AI agents inspect a running TIA Portal project through the Siemens Openness API.

The server currently exposes one tool:

- `browse_project_tree` - recursively enumerates TIA devices, PLC software, program blocks, PLC tags, and PLC data types, returning a JSON project tree.

## Architecture

TIA Portal V21 ships its Openness API as .NET Framework 4.8 assemblies. Those assemblies use .NET Framework remoting APIs that cannot run correctly inside a .NET 8 process.

This project therefore uses two processes:

- `TiaMcpServer` - the .NET 8 MCP stdio server and .NET global tool host.
- `TiaMcpServer.OpennessWorker` - a .NET Framework 4.8 worker process that loads `Siemens.Engineering.*` and talks to TIA Portal.

The MCP host starts the worker on demand and exchanges newline-delimited JSON over stdin/stdout. Siemens DLLs are never copied into this repository or the NuGet package; the worker resolves them from the local TIA Portal V21 installation.

## Requirements

- Windows
- Siemens TIA Portal V21 installed
- TIA Portal Openness installed and enabled
- Current Windows user is a member of the `Siemens TIA Openness` user group
- .NET SDK 8 or newer
- .NET Framework 4.8 Developer Pack or targeting pack

By default, the build expects Openness DLLs here:

```text
C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48
```

You can override that path with the `TiaPortalV21Dir` MSBuild property or environment variable. It must point to the folder containing `Siemens.Engineering.Base.dll` and `Siemens.Engineering.Step7.dll`.

## Build

```powershell
dotnet restore TiaMcpServer.sln
dotnet build TiaMcpServer.sln
```

The build creates the .NET 8 host and copies the .NET Framework worker into:

```text
TiaMcpServer\bin\Debug\net8.0\openness-worker
```

## Run Locally

Start TIA Portal V21 first and open a project, then run:

```powershell
dotnet run --project TiaMcpServer
```

The server uses MCP over stdio, so it is normally launched by an MCP client rather than used interactively in a terminal.

You can test the Openness worker directly:

```powershell
'{ "method": "browse_project_tree", "projectPath": null }' | .\TiaMcpServer.OpennessWorker\bin\Debug\net48\TiaMcpServer.OpennessWorker.exe
```

Expected successful response shape:

```json
{"success":true,"payload":"[...]"}
```

Expected error response shape:

```json
{"success":false,"error":"No running TIA Portal V21 instance found. Please start TIA Portal before using the MCP server."}
```

## Pack And Install

Create a local tool package:

```powershell
dotnet pack TiaMcpServer\TiaMcpServer.csproj -c Release
```

Install from the generated package source:

```powershell
dotnet tool install -g TiaMcpServer --add-source .\TiaMcpServer\bin\Release
```

Run the installed server:

```powershell
tia-mcp-server
```

The package includes the `openness-worker` folder and required non-Siemens dependencies. It intentionally excludes `Siemens.Engineering*.dll`; those are loaded from the local TIA Portal installation at runtime.

## Roadmap

Current status:

- Phase 1 is implemented: MCP stdio host, TIA Openness worker bridge, and `browse_project_tree` project discovery.

Planned next phases:

- Phase 2: universal block support through V21 YAML export/import, including SCL, LAD, FBD, Graph, STL, global DBs, and instance DBs.
- Phase 3: hardware and network discovery, including rack configuration, I/O modules, PROFINET device names, and Network View data where available through Openness.
- Phase 4: advanced diagnostics, including compile/check-syntax results and cross-reference style project analysis for AI-assisted review.

Publishing intent:

- Publish as a .NET global tool on NuGet under the `TiaMcpServer` package ID.
- Keep installation to a single command once published:

```powershell
dotnet tool install -g TiaMcpServer
```

- Keep the packaged tool self-contained except for Siemens Openness assemblies, which must continue to resolve from the local TIA Portal V21 installation.
- Add an optional `--setup` flow later to help auto-register the MCP server in supported AI client configurations.

## MCP Client Configuration

Configure your MCP client to launch the tool command:

```json
{
  "mcpServers": {
    "tia-portal": {
      "command": "tia-mcp-server"
    }
  }
}
```

For local development without installing the tool, point the client at `dotnet`:

```json
{
  "mcpServers": {
    "tia-portal-dev": {
      "command": "dotnet",
      "args": ["run", "--project", "{PROJECT PATH}"]
    }
  }
}
```

## Troubleshooting

- `System.Runtime.Remoting.RemotingException` / `TypeLoadException` in .NET 8: Siemens Openness must run in the net48 worker. Rebuild the solution and make sure the host output contains `openness-worker\TiaMcpServer.OpennessWorker.exe`.
- Openness DLL not found: verify TIA Portal V21 is installed and set `TiaPortalV21Dir` to the `PublicAPI\V21\net48` folder if your install path is non-standard.
- No running TIA Portal instance: start TIA Portal V21 before calling `browse_project_tree`.
- Access denied or attach failure: confirm the Windows user belongs to the `Siemens TIA Openness` user group, then sign out and back in.
