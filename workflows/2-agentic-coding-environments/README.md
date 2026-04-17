# Agentic Coding Environments — D365 CE Workflows

CLI and AI-first IDE agents capable of autonomous, multi-step coding tasks including generating, running, and iterating on CI/CD pipelines and GitHub Actions workflows.

**Primary tools:** Claude Code (CLI), Cursor, Windsurf, Aider

---

## AI-Powered Code Review Automation (GitHub Actions)

**Concept:** Integrate a GitHub Actions workflow that runs on Pull Requests and uses an AI API call to review plugin or Azure Function changes against internal best practices before a human reviewer picks up the PR.

**Generate the action:**
```
Create a GitHub Actions workflow that triggers on pull requests targeting main.
For each changed .cs file, call the Anthropic API to review the code
against these rules: [paste rules from AGENTS.md or best-practices.md].
Post the findings as a PR comment. Use a repository secret for the API key.
```
