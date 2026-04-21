# AI Tooling for Dynamics 365 CE & Power Platform

A curated repository of AI workflows, prompts, configurations, and references organized around a practical taxonomy of AI tooling. The goal is to help development teams achieve greater productivity and higher quality during implementation and maintenance of Microsoft Dynamics 365 Customer Engagement and the broader Power Platform.

---

## Principles

- Implement solutions that work and are genuinely helpful to the end user
- Deliver best-in-class quality and follow established conventions and best practices
- Minimize rework and cost
- Leverage AI as a productivity multiplier, not a replacement for judgment
- Maintain Human-in-the-Loop (HITL) principles throughout
- Integrate with the broader Development ecosystem: specially Microsoft tooling and SaaS - Azure, Visual Studio, VS Code, GitHub, ADO

---

## AI Tool Taxonomy

Tools are organized by their **deployment context and execution model** — that is, where the AI runs, how it integrates into a workflow, how it is context aware, and what kind of actions it can take. This is distinct from autonomy-level classifications (chatbot → copilot → agent → digital employee)[^1], which describe *how much* an AI can do, not *where or how* it operates. An example of this type of comparison for AI tools is maintained by Artificial Analysis[^2].

In the specific case of consulting, project and development work the **main issue for AI tooling is how to provide context**. AI is not useful if it is not aware of things like:
- Phase within a plan (are we planning and no work has been done yet? are we in maintenance mode and we have a multi-year user base? somewhere in the middle?)
- What's already built (are there components already in place? what columns does this table have? artifacts already built)
- Overall design (which can be at the Enterprise level, module level, and specific design patterns to be used)
- Plans in place (epics, features, user stories, documentation that describes from a functional perspective what has been done, what's planned to be built and when)
- Existing constraints (which tools can be used, security restrictions, licensing, budget, capacity;  is there a code freeze?)
- Tools (including development, design, project management and other tools)

This **context** is the grounding that an AI needs to provide answers and build artifacts that work. Tools are then classified by how they can operate in context, and the actions that they can carry out.

### 1. Coding Assistants & Agentic Environments

AI tools integrated into a developer's working environment — either embedded in an existing IDE/editor or operating as a dedicated AI-first IDE or CLI agent. Provide inline suggestion, chat, fully autonomous multi-file, multi-step task execution with build and test feedback loops.

**Characteristics:** inline suggestions, chat within editor, code explanation, refactoring, test generation, terminal execution, repo-level reasoning, multi-file edits, test/build feedback loops  
**Examples:** GitHub Copilot (VS Code)[^3], Cline, Continue, JetBrains AI Assistant, Claude Code (CLI)[^4], Cursor (dedicated IDE)[^5], Windsurf, Aider  
**D365 relevance:** Plugin development, PCF controls, Azure Functions, JavaScript web resources, scaffolding plugin solutions, generating and running unit tests, refactoring assemblies, ADO pipeline authoring

---

### 2. General-Purpose AI Assistants

Web, desktop, or mobile chat interfaces offering broad reasoning, generation, and task support. Not specialized to a specific execution environment. Agentic capabilities (tool use, browsing, code execution) may be available depending on the platform.

**Characteristics:** conversational, broad knowledge, document generation, research, planning  
**Examples:** Claude.ai, ChatGPT, Gemini, Microsoft 365 Copilot (chat mode)  
**D365 relevance:** Requirements analysis, writing user stories and acceptance criteria, architecture documentation, review of solution designs, drafting runbooks

---

### 3. Local / Desktop Automation Agents

AI with direct access to the local operating system, filesystem, browser, and installed applications. Executes tasks across the desktop environment with computer-use capabilities. Best suited for multi-application workflows that cannot be automated through APIs alone.

**Characteristics:** browser control, file system access, UI interaction, cross-application automation  
**Examples:** Claude Cowork, Anthropic Computer Use[^6], OpenAI Operator  
**D365 relevance:** Automating repetitive configuration tasks in make.powerapps.com, navigating Power Platform Admin Center, extracting data from XrmToolbox outputs

---

### 4. Platform-Embedded Copilots

AI tightly integrated into a specific SaaS or enterprise platform, surfaced within that platform's native UX. Actions are scoped to that platform's data model, permissions, and APIs. These are consumption experiences, not authoring environments.

**Characteristics:** platform-native, context-aware of platform data, no external tool access  
**Examples:** Microsoft 365 Copilot (Word/Excel/Teams), Copilot for Dynamics 365, Copilot in Power Apps (natural language table/form generation)  
**D365 relevance:** Drafting solution documentation directly in SharePoint, summarizing CRM records, generating Power Fx expressions, AI Builder model integration

---

### 5. Agent Builder Frameworks / Orchestration Platforms

Low-code or pro-code platforms for composing, deploying, and governing AI agents with defined instructions, topics, connectors, actions, and handoff logic. The output of working in these platforms is a deployable agent, not a one-off conversation.

**Characteristics:** topic/instruction authoring, connector integration, multi-agent orchestration, governance  
**Examples:** Copilot Studio[^7], Azure AI Foundry[^8], Semantic Kernel[^9], AutoGen[^10], LangChain  
**D365 relevance:** Building D365-connected virtual agents, orchestrating multi-step business processes, custom copilots for field service or sales, autonomous agents triggered by Dataverse events

---

### 6. AI-Augmented Specialty Tools

Domain-specific tools that have embedded AI into their existing UX rather than being AI-first products. The AI enhances a pre-existing workflow tool's native capabilities.

**Characteristics:** AI features embedded in established tooling, domain-specific context  
**Examples:** XrmToolbox plugins with AI capabilities, GitHub Copilot for Pull Requests, ADO Copilot features, Postman AI  
**D365 relevance:** AI-assisted FetchXML authoring, automated PR descriptions for solution changes, test case generation in ADO

---

## Repository Structure

| Path | Description |
|------|-------------|
| [Docs](docs/) | Reference documentation, architecture notes, and decision records |
| [Scripts](scripts/) | Utility scripts supporting AI-assisted tasks and automation |
| [1. Coding Assistants & Agentic Environments](workflows/1-coding-assistants/README.md) | Workflows using IDE-embedded and CLI/agentic coding AI (e.g., GitHub Copilot, Claude Code, Cursor) |
| [2. General-Purpose AI Assistants](workflows/2-general-purpose-assistants/README.md) | Workflows using web/desktop chat AI (e.g., Claude.ai, ChatGPT) |
| [Pending](workflows/3-desktop-automation-agents/) | Workflows using local OS/browser automation agents (e.g., Computer Use) |
| [Pending](workflows/4-platform-embedded-copilots/) | Workflows using platform-native copilots (e.g., M365 Copilot, D365 Copilot) |
| [Pending](workflows/5-agent-builder-frameworks/) | Workflows using agent orchestration platforms (e.g., Copilot Studio, Foundry) |
| [Pending](workflows/6-ai-augmented-specialty-tools/) | Workflows using AI-enhanced domain tools (e.g., XrmToolbox, GitHub PRs) |

---

## Notes on Taxonomy

This classification is organized by *deployment context and execution model*, not purely by autonomy level. A single AI model (e.g., Claude or GPT-4o) may appear in multiple categories depending on the surface through which it is accessed — for example, Claude accessed via Claude.ai (Category 2), via Claude Code in a terminal (Category 1), or via a Copilot Studio integration (Category 5) represents three different tool categories despite sharing the same underlying model.

This distinction matters for governance, security review, and workflow design: the *execution environment* determines what the AI can act on, what data it can access, and what oversight is appropriate.

Note that within Category 1, the spectrum runs from suggestion-driven assistance (IDE extensions like GitHub Copilot) to fully autonomous multi-step execution (CLI agents like Claude Code and dedicated AI-first IDEs like Cursor). Both share the characteristic of operating within a developer's working environment.

---

## References

[^1]: Taskade — [AI Agents Taxonomy: Chatbot, Copilot, Agent, Digital Employee](https://www.taskade.com/blog/ai-agents-taxonomy). Defines the four-tier autonomy ladder with distinct characteristics for each level.

[^2]: Artificial Analysis — [Coding Agent Comparison](https://artificialanalysis.ai/agents/coding). Organizes coding AI tools by deployment category: IDE extensions, dedicated IDEs, CLI tools, and cloud platforms.

[^3]: Microsoft / Visual Studio Code — [GitHub Copilot in VS Code](https://code.visualstudio.com/docs/copilot/overview). Official documentation covering inline suggestions, chat, and agentic capabilities within VS Code.

[^4]: Anthropic — [Claude Code Overview](https://docs.anthropic.com/en/docs/claude-code/overview). Official documentation describing Claude Code as an agentic CLI tool that reads codebases, edits files, and runs commands.

[^5]: Cursor — [Cursor: The AI Code Editor](https://cursor.com/). Official site for Cursor, an AI-first IDE with architecturally integrated AI and agent (Composer) capabilities.

[^6]: Anthropic — [Computer Use Tool](https://docs.anthropic.com/en/docs/agents-and-tools/computer-use). Official API documentation for Claude's computer-use capability (screenshot capture, mouse/keyboard control, desktop automation).

[^7]: Microsoft Learn — [Declarative Agents for Microsoft 365 Copilot](https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/overview-declarative-agent). Covers both declarative agents (instructions + knowledge) and autonomous agents with event-based execution in Copilot Studio.

[^8]: Microsoft Learn — [Azure AI Foundry Agent Service](https://learn.microsoft.com/en-us/azure/foundry/agents/overview). Documents Foundry as a fully managed platform for building, deploying, and scaling AI agents with multi-agent orchestration support.

[^9]: Microsoft Learn — [Semantic Kernel Agent Orchestration](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/). Documents Semantic Kernel's orchestration patterns (Sequential, Concurrent, Handoff, Group Chat, Magentic) for multi-agent workflows.

[^10]: Microsoft — [AutoGen on GitHub](https://github.com/microsoft/autogen). Microsoft's open-source framework for multi-agent AI applications; currently in maintenance mode with recommendation to use Microsoft Agent Framework for new projects.
