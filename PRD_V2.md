# Product Requirements Document (PRD): TIA Portal V21 MCP Server

## 1. Project Overview

The **TIA Portal V21 MCP Server** is a professional-grade bridge implementing the **Model Context Protocol (MCP)**. It exposes the full depth of a Siemens SIMATIC TIA Portal V21 project to AI agents. Unlike narrow implementations, this server provides universal access to all PLC programming languages and hardware configurations, empowering AI to perform cross-language code analysis, hardware audits, and full-scale project manipulation.

## 2. Core Philosophy

* **Agnosticism:** The server does not filter data based on perceived "AI compatibility." It provides the raw project data (via YAML/Openness) and lets the LLM/User manage token limits.
* **Independence:** This is a standalone tool. It requires only TIA Portal V21 and the .NET runtime.
* **Transparency:** Every interaction between the AI and the PLC project is logged and subject to user-defined safety constraints.

## 3. Technical Stack

* **Primary Language:** C# (.NET 8.0)
* **API:** Siemens.Engineering V21 (TIA Openness)
* **Protocol:** MCP (Model Context Protocol) via Standard Input/Output (stdio)
* **Data Exchange Format:** SIMATIC Source Documents (YAML) for blocks; JSON-RPC for protocol.

---

## 4. Functional Requirements

### 4.1. Universal Block Support

The server must provide tools to read, write, and list blocks in all supported TIA Portal languages:

* **Structured Text:** SCL
* **Graphical:** LAD (Ladder), FBD (Function Block Diagram), Graph
* **Low-Level:** STL (Statement List)
* **Data:** DB (Global and Instance Data Blocks)

### 4.2. Project Discovery Tools

* **`browse_project_tree`**: Recursively list folders, Software Units, and PLC types.
* **`get_block_content`**: Export any block to its V21 YAML representation.
* **`list_tag_tables`**: Retrieve all PLC tags and constants across all tables.
* **`read_hardware_config`**: Export the rack configuration, I/O modules, and PROFINET device names.

### 4.3. Project Modification Tools

* **`update_block_logic`**: Import a YAML-formatted source to update or create a block.
* **`sync_tags`**: Create or modify tags within a specified Tag Table.
* **`compile_check`**: Invoke the Openness "Check Syntax" or "Compile" method and return errors/warnings to the AI.

---

## 5. Installation & Distribution

To ensure a "single-command" onboarding experience:

* **Distribution:** Packaged as a **.NET Global Tool** published to NuGet.
* **Installation:**

```powershell
    dotnet tool install -g TiaMcpServer
```

* **Auto-Config:** Upon first run, the server can optionally execute a `--setup` flag that auto-locates the user's AI client configuration (e.g., Claude Desktop) and adds the server entry.

---

## 6. Security & Guardrails

* **Access Control:** Must run within the security context of the `Siemens TIA Openness` Windows user group.
* **Integrity:** The server will implement a "Human-In-The-Loop" flag. When enabled, any "write" operation will prompt the user for confirmation via a CLI toast or console input before committing to the `.ap21` file.
* **Session Isolation:** Each MCP session is bound to a single project instance to prevent cross-contamination of code between different customer projects.

---

## 7. Roadmap

* **Phase 1:** Core MCP/Openness handshake and Project Tree discovery.
* **Phase 2:** Full YAML export/import for all block types (Universal Support).
* **Phase 3:** Hardware configuration and Network View discovery.
* **Phase 4:** Advanced diagnostics (reading the cross-reference list via AI).
