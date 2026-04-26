# Graph Report - .  (2026-04-26)

## Corpus Check
- Corpus is ~2,546 words - fits in a single context window. You may not need a graph.

## Summary
- 76 nodes · 92 edges · 12 communities detected
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 6 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_TIA Portal Session Management|TIA Portal Session Management]]
- [[_COMMUNITY_Worker Entry Point & Request Handling|Worker Entry Point & Request Handling]]
- [[_COMMUNITY_TIA Project Tree Traversal|TIA Project Tree Traversal]]
- [[_COMMUNITY_Project Overview & Roadmap|Project Overview & Roadmap]]
- [[_COMMUNITY_Worker Client (Host Side)|Worker Client (Host Side)]]
- [[_COMMUNITY_Assembly Resolution (Worker Side)|Assembly Resolution (Worker Side)]]
- [[_COMMUNITY_Multi-process Architecture & Rationale|Multi-process Architecture & Rationale]]
- [[_COMMUNITY_MCP Server Tools|MCP Server Tools]]
- [[_COMMUNITY_Project Tree Data Contract|Project Tree Data Contract]]
- [[_COMMUNITY_Worker Request Contract|Worker Request Contract]]
- [[_COMMUNITY_Worker Response Contract|Worker Response Contract]]
- [[_COMMUNITY_Siemens TIA Portal V21 (External)|Siemens TIA Portal V21 (External)]]

## God Nodes (most connected - your core abstractions)
1. `TiaPortalSession` - 11 edges
2. `ProjectTreeWalker` - 9 edges
3. `Program` - 8 edges
4. `tia-portal-mcp` - 8 edges
5. `OpennessWorkerClient` - 6 edges
6. `AssemblyResolver` - 6 edges
7. `TiaMcpServer.OpennessWorker (.NET 4.8 worker)` - 4 edges
8. `BrowseProjectTreeTool` - 3 edges
9. `TiaMcpServer (.NET 8 host)` - 3 edges
10. `Multi-process Architecture Rationale` - 3 edges

## Surprising Connections (you probably didn't know these)
- `tia-portal-mcp` --references--> `Siemens Openness API`  [EXTRACTED]
  README.md → README.md  _Bridges community 3 → community 6_

## Hyperedges (group relationships)
- **Multi-process Bridge** — readme_server_host, readme_worker_process [EXTRACTED 1.00]

## Communities

### Community 0 - "TIA Portal Session Management"
Cohesion: 0.24
Nodes (2): IDisposable, TiaPortalSession

### Community 1 - "Worker Entry Point & Request Handling"
Cohesion: 0.31
Nodes (2): Program, TiaMcpServer

### Community 2 - "TIA Project Tree Traversal"
Cohesion: 0.33
Nodes (1): ProjectTreeWalker

### Community 3 - "Project Overview & Roadmap"
Cohesion: 0.25
Nodes (8): browse_project_tree tool, Model Context Protocol, Siemens TIA Openness User Group, Phase 1: Implementation, Phase 2: Universal Block Support, Phase 3: Hardware and Network Discovery, Phase 4: Advanced Diagnostics, tia-portal-mcp

### Community 4 - "Worker Client (Host Side)"
Cohesion: 0.43
Nodes (1): OpennessWorkerClient

### Community 5 - "Assembly Resolution (Worker Side)"
Cohesion: 0.38
Nodes (1): AssemblyResolver

### Community 6 - "Multi-process Architecture & Rationale"
Cohesion: 0.33
Nodes (7): Multi-process Architecture Rationale, .NET Framework 4.8, .NET 8, Siemens Openness API, .NET Remoting Incompatibility in .NET 8, TiaMcpServer (.NET 8 host), TiaMcpServer.OpennessWorker (.NET 4.8 worker)

### Community 7 - "MCP Server Tools"
Cohesion: 0.5
Nodes (2): BrowseProjectTreeTool, TiaMcpServer.Tools

### Community 8 - "Project Tree Data Contract"
Cohesion: 0.67
Nodes (1): ProjectTreeNode

### Community 9 - "Worker Request Contract"
Cohesion: 0.67
Nodes (1): WorkerRequest

### Community 10 - "Worker Response Contract"
Cohesion: 0.67
Nodes (1): WorkerResponse

### Community 11 - "Siemens TIA Portal V21 (External)"
Cohesion: 1.0
Nodes (1): Siemens SIMATIC TIA Portal V21

## Knowledge Gaps
- **11 isolated node(s):** `Siemens SIMATIC TIA Portal V21`, `Model Context Protocol`, `browse_project_tree tool`, `.NET Remoting Incompatibility in .NET 8`, `Phase 1: Implementation` (+6 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `TIA Portal Session Management`** (12 nodes): `TiaPortalSession.cs`, `IDisposable`, `TiaPortalSession.cs`, `TiaPortalSession`, `.Connect()`, `.Dispose()`, `.EnsureConnected()`, `.OnConfirmation()`, `.OnDisposed()`, `.OnNotification()`, `.OpenProject()`, `.ThrowIfDisposed()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Worker Entry Point & Request Handling`** (10 nodes): `Program.cs`, `Program.cs`, `Program`, `.BrowseProjectTree()`, `.Failure()`, `.HandleLine()`, `.Main()`, `TiaMcpServer`, `Program.cs`, `Program.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `TIA Project Tree Traversal`** (10 nodes): `ProjectTreeWalker.cs`, `ProjectTreeWalker`, `.FindPlcSoftwareInDevice()`, `.FindPlcSoftwareInDeviceItems()`, `.Walk()`, `.WalkBlockGroup()`, `.WalkPlcSoftware()`, `.WalkTagTableGroup()`, `.WalkTypeGroup()`, `ProjectTreeWalker.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Worker Client (Host Side)`** (7 nodes): `OpennessWorkerClient.cs`, `OpennessWorkerClient`, `.BrowseProjectTreeAsync()`, `.LocateWorkerExecutable()`, `.SendAsync()`, `.TryKill()`, `OpennessWorkerClient.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Assembly Resolution (Worker Side)`** (7 nodes): `AssemblyResolver`, `.ContainsRequiredAssemblies()`, `.GetOpennessInstallPath()`, `.OnAssemblyResolve()`, `.Register()`, `AssemblyResolver.cs`, `AssemblyResolver.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `MCP Server Tools`** (5 nodes): `BrowseProjectTreeTool`, `.BrowseProjectTree()`, `TiaMcpServer.Tools`, `BrowseProjectTreeTool.cs`, `BrowseProjectTreeTool.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Project Tree Data Contract`** (3 nodes): `ProjectTreeNode.cs`, `ProjectTreeNode`, `ProjectTreeNode.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Worker Request Contract`** (3 nodes): `WorkerRequest.cs`, `WorkerRequest.cs`, `WorkerRequest`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Worker Response Contract`** (3 nodes): `WorkerResponse.cs`, `WorkerResponse.cs`, `WorkerResponse`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Siemens TIA Portal V21 (External)`** (1 nodes): `Siemens SIMATIC TIA Portal V21`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What connects `Siemens SIMATIC TIA Portal V21`, `Model Context Protocol`, `browse_project_tree tool` to the rest of the system?**
  _11 weakly-connected nodes found - possible documentation gaps or missing edges._