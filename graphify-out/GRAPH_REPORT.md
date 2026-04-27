# Graph Report - tia-portal-mcp  (2026-04-27)

## Corpus Check
- 24 files · ~6,662 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 144 nodes · 216 edges · 22 communities detected
- Extraction: 84% EXTRACTED · 16% INFERRED · 0% AMBIGUOUS · INFERRED: 35 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]

## God Nodes (most connected - your core abstractions)
1. `Program` - 10 edges
2. `BlockTargetResolver` - 10 edges
3. `ProjectTreeWalker` - 10 edges
4. `TiaPortalSession` - 10 edges
5. `OpennessWorkerClient` - 8 edges
6. `TagTableReader` - 8 edges
7. `BlockAddress` - 7 edges
8. `BlockAddressTests` - 7 edges
9. `tia-portal-mcp` - 6 edges
10. `AssemblyResolver` - 5 edges

## Surprising Connections (you probably didn't know these)
- None detected - all connections are within the same source files.

## Communities

### Community 0 - "Community 0"
Cohesion: 0.18
Nodes (4): IDisposable, Program, TiaMcpServer, TiaPortalSession

### Community 1 - "Community 1"
Cohesion: 0.18
Nodes (3): OpennessWorkerClient, ProjectSessionBinding, ProjectSessionBindingTests

### Community 2 - "Community 2"
Cohesion: 0.21
Nodes (2): BlockAddress, BlockAddressTests

### Community 3 - "Community 3"
Cohesion: 0.23
Nodes (3): BlockExporter, BlockTargetResolver, ResolvedBlockTarget

### Community 4 - "Community 4"
Cohesion: 0.45
Nodes (1): ProjectTreeWalker

### Community 5 - "Community 5"
Cohesion: 0.39
Nodes (1): TagTableReader

### Community 6 - "Community 6"
Cohesion: 0.29
Nodes (7): Model Context Protocol, Siemens TIA Openness User Group, Phase 1: Implementation, Phase 2: Universal Block Support, Phase 3: Hardware and Network Discovery, Phase 4: Advanced Diagnostics, tia-portal-mcp

### Community 7 - "Community 7"
Cohesion: 0.47
Nodes (1): AssemblyResolver

### Community 8 - "Community 8"
Cohesion: 0.53
Nodes (1): BlockImporter

### Community 9 - "Community 9"
Cohesion: 0.5
Nodes (2): BrowseProjectTreeTool, TiaMcpServer.Tools

### Community 10 - "Community 10"
Cohesion: 0.5
Nodes (2): GetBlockContentTool, TiaMcpServer.Tools

### Community 11 - "Community 11"
Cohesion: 0.5
Nodes (2): ListTagTablesTool, TiaMcpServer.Tools

### Community 12 - "Community 12"
Cohesion: 0.5
Nodes (2): TiaMcpServer.Tools, UpdateBlockLogicTool

### Community 13 - "Community 13"
Cohesion: 1.0
Nodes (1): ProjectTreeNode

### Community 14 - "Community 14"
Cohesion: 1.0
Nodes (1): TagInfo

### Community 15 - "Community 15"
Cohesion: 1.0
Nodes (1): TagTableInfo

### Community 16 - "Community 16"
Cohesion: 1.0
Nodes (1): UserConstantInfo

### Community 17 - "Community 17"
Cohesion: 1.0
Nodes (1): WorkerRequest

### Community 18 - "Community 18"
Cohesion: 1.0
Nodes (1): WorkerResponse

### Community 19 - "Community 19"
Cohesion: 1.0
Nodes (2): Multi-process Architecture Rationale, .NET Remoting Incompatibility in .NET 8

### Community 20 - "Community 20"
Cohesion: 1.0
Nodes (1): .NET 8

### Community 21 - "Community 21"
Cohesion: 1.0
Nodes (1): .NET Framework 4.8

## Knowledge Gaps
- **22 isolated node(s):** `TiaMcpServer`, `TiaMcpServer.Tools`, `TiaMcpServer.Tools`, `TiaMcpServer.Tools`, `TiaMcpServer.Tools` (+17 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `Community 2`** (16 nodes): `BlockAddress`, `.FromBlockSegments()`, `.IsReservedSegment()`, `.Parse()`, `.SplitSegments()`, `.StripBlockSuffix()`, `.ToDisplayPath()`, `BlockAddressTests`, `.ParseRejectsInvalidPaths()`, `.ParseStripsBlockSuffixFromFinalSegment()`, `.ParseSupportsLegacyBlockOnly()`, `.ParseSupportsLegacyPlcQualifiedBlock()`, `.ParseSupportsNestedBlockFolderPath()`, `.ParseSupportsSoftwareUnitBlockPath()`, `BlockAddress.cs`, `BlockAddressTests.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 4`** (11 nodes): `ProjectTreeWalker`, `.CombinePath()`, `.FindPlcSoftwareInDevice()`, `.FindPlcSoftwareInDeviceItems()`, `.Walk()`, `.WalkBlockGroup()`, `.WalkPlcSoftware()`, `.WalkSoftwareUnits()`, `.WalkTagTableGroup()`, `.WalkTypeGroup()`, `ProjectTreeWalker.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 5`** (9 nodes): `TagTableReader`, `.CollectTablesFromGroup()`, `.FindPlcSoftware()`, `.FindPlcSoftwareInDeviceItems()`, `.ReadAll()`, `.ReadTags()`, `.ReadTagTable()`, `.ReadUserConstants()`, `TagTableReader.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 7`** (6 nodes): `AssemblyResolver`, `.ContainsRequiredAssemblies()`, `.GetOpennessInstallPath()`, `.OnAssemblyResolve()`, `.Register()`, `AssemblyResolver.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 8`** (6 nodes): `BlockImporter`, `.ExtractFileName()`, `.FlushSection()`, `.Import()`, `.WriteContentToTempDir()`, `BlockImporter.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 9`** (4 nodes): `BrowseProjectTreeTool`, `.BrowseProjectTree()`, `TiaMcpServer.Tools`, `BrowseProjectTreeTool.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 10`** (4 nodes): `GetBlockContentTool`, `.GetBlockContent()`, `TiaMcpServer.Tools`, `GetBlockContentTool.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 11`** (4 nodes): `ListTagTablesTool`, `.ListTagTables()`, `TiaMcpServer.Tools`, `ListTagTablesTool.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 12`** (4 nodes): `UpdateBlockLogicTool.cs`, `TiaMcpServer.Tools`, `UpdateBlockLogicTool`, `.UpdateBlockLogic()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 13`** (2 nodes): `ProjectTreeNode`, `ProjectTreeNode.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 14`** (2 nodes): `TagInfo`, `TagInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 15`** (2 nodes): `TagTableInfo`, `TagTableInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 16`** (2 nodes): `UserConstantInfo.cs`, `UserConstantInfo`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 17`** (2 nodes): `WorkerRequest.cs`, `WorkerRequest`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 18`** (2 nodes): `WorkerResponse.cs`, `WorkerResponse`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 19`** (2 nodes): `Multi-process Architecture Rationale`, `.NET Remoting Incompatibility in .NET 8`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 20`** (1 nodes): `.NET 8`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 21`** (1 nodes): `.NET Framework 4.8`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What connects `TiaMcpServer`, `TiaMcpServer.Tools`, `TiaMcpServer.Tools` to the rest of the system?**
  _22 weakly-connected nodes found - possible documentation gaps or missing edges._