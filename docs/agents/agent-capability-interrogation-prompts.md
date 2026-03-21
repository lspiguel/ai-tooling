# Agent Capability Interrogation: A Reusable Prompt Sequence

## Purpose

This document provides a structured sequence of prompts for interrogating any AI agent to
document its capabilities, infrastructure, tools, and limitations. The sequence combines
declarative questions (asking the agent to describe itself) with empirical probing (running
commands and observing actual behaviour) to produce a reliable, verifiable capability profile.

Use this sequence at the start of any agent evaluation. Results can then be compiled into a
reference document for that specific agent.

-----

## How to Use This Guide

- Run prompts in order — earlier answers inform later questions.
- Do not skip empirical tests; agents often underreport or overreport capabilities in
  declarative questions alone.
- After each section, push back with "Is that list exhaustive?" or "Are there any exceptions
  to what you just described?" — agents frequently surface additional detail on follow-up.
- At the end, ask the agent to compile its own findings into a structured document. This
  forces synthesis and often reveals gaps or contradictions.

-----

## Section 1: Identity and Purpose

These prompts establish the agent's role and scope before probing specifics.

```
1. What kind of agent are you? Describe your primary purpose and the types of tasks
   you are designed to assist with.

2. What platform or product are you part of (e.g. a chat interface, a VS Code extension,
   a CLI tool, an API)? Does your context affect what you can do?

3. Does your capability set change depending on the model selected (e.g. GPT-4 vs GPT-3.5,
   Claude Opus vs Sonnet, etc.)? If so, how?
```

**Why:** Establishes baseline framing. Some agents are narrowly scoped (IDE assistants,
customer support bots) while others are broadly agentic. Knowing this shapes how to interpret
later answers.

-----

## Section 2: Execution Environment

These prompts determine whether the agent has access to a live compute environment.

```
4. Do you have access to a live execution environment where you can run code (e.g. a bash
   shell, a Python interpreter, a Node.js runtime)? If yes, is this running on cloud
   infrastructure, or on my local machine?

5. What operating system and version is the execution environment? Is it containerised
   (e.g. Docker)? Can you confirm this empirically by running a command?

6. Run: uname -a
   What does the output tell you about the environment?

7. Run: ls -lar /
   What notable files and directories are at the root? Are there any non-standard entries
   that reveal something about the infrastructure (e.g. .dockerenv, process API executables,
   metadata files)?

8. Run: ls /usr
   Describe the directory structure. Does it match a standard distribution layout?

9. Run: whoami && id
   What user is the agent running as? What privileges does it have?

10. Run: cat /proc/version 2>/dev/null || sw_vers 2>/dev/null || systeminfo 2>/dev/null
    What additional OS and kernel details are available?
```

**Why:** Declarative answers about the environment are often hedged or incomplete. Running
`ls -lar /` on the Claude environment revealed a `.dockerenv` file (confirming Docker), a
`process_api` executable (API communication layer), and a `container_info.json` metadata file
— none of which Claude described unprompted.

-----

## Section 3: State and Persistence

These prompts probe the session model — critical for planning multi-step workflows.

```
11. Does state persist between prompts within a single conversation? For example, if you
    create a file in one message, will it still exist when I send my next message?

12. Does state persist between separate conversations (i.e. if I close this chat and open
    a new one, is anything carried over)?

13. Now let's test this empirically:
    Run: echo "persistence_test" > /tmp/session_probe.txt && cat /tmp/session_probe.txt
    Confirm the file was created.

[Send a follow-up prompt in the same conversation:]

14. Run: cat /tmp/session_probe.txt
    Is the file still there? What does this tell us about within-conversation state persistence?

15. Is there any form of persistent memory that survives across conversations? If yes,
    how is it stored and managed? Can I view or edit it?
```

**Why:** Claude initially expressed uncertainty about within-conversation persistence. Empirical
testing confirmed that state does persist. The Copilot agent, by contrast, runs locally with a
different model entirely. Testing reveals the truth faster than asking.

-----

## Section 4: File System Access

These prompts map what the agent can read, write, and where.

```
16. What parts of the file system can you read? Are there any paths that are read-only,
    write-only, or inaccessible?

17. What working directories are available for creating temporary files?

18. Are there any special mounted directories (e.g. /mnt/, ~/uploads/) that give you
    access to user-provided files or outputs?

19. Run: ls /mnt/ 2>/dev/null || echo "not found"
    What is mounted? What do those paths suggest about how files are shared between
    the agent and the user?

20. Can you write files to my local machine's file system directly, or only to your own
    environment? How do files get from your environment to me?
```

**Why:** Understanding the file system layout is essential for any file-processing workflow.
The Claude environment uses `/mnt/user-data/uploads/` and `/mnt/user-data/outputs/` as the
bridge between the agent's container and the user.

-----

## Section 5: Network and Connectivity

These prompts determine the agent's access to the outside world.

```
21. Do you have network access from your execution environment? Can you make outbound
    HTTP requests, connect to external APIs, or download files from the internet?

22. Run: curl -s --max-time 3 https://example.com 2>&1 || echo "curl not available"
    Run: wget -q --timeout=3 -O - https://example.com 2>&1 || echo "wget not available"
    What do the results tell you?

23. Run: ifconfig 2>/dev/null || ip addr 2>/dev/null || echo "network tools not available"
    Are standard network inspection tools available?

24. Separately from the execution environment: do you have tools that can access the web
    (e.g. a web_search or web_fetch tool that routes through a managed proxy)? How does
    this differ from direct network access in the container?
```

**Why:** There is often a distinction between network access in the execution environment
(usually disabled for security) and managed web-access tools (search, fetch) that are
explicitly provided as safe abstractions. Both the Claude and Copilot agents had this split,
and it is easy to conflate them.

-----

## Section 6: Tool Inventory — Initial Pass

```
25. What tools do you have available? Please provide an initial overview grouped by category.

26. Is that list exhaustive? Are there any tools you have access to that you did not mention —
    including tools that are deferred, loaded on demand, or conditionally available?

27. Are there any meta-tools that allow you to query or discover what tools are available
    at runtime?
```

**Why:** Agents consistently provide incomplete lists on the first pass. The follow-up "is
that exhaustive?" question surfaced deferred calendar/reminder tools in the Claude session and
additional VS Code tools in the Copilot session.

-----

## Section 7: Tool Inventory — Deep Documentation

Run this after getting the full list from Section 6.

```
28. For each tool you have listed, please provide:
    - Tool name
    - Purpose (one sentence)
    - Key parameters (name, type, what it controls)
    - What it returns or outputs
    - Important limitations or caveats
    - A concrete example of when you would use it

    Group them by category. Format the output as a structured markdown document.

29. For tools that are deferred or loaded on demand: what triggers loading them? Do you
    need to call a meta-tool first, or are they loaded automatically when needed?

30. For each tool that accesses external services (web, calendar, location, etc.):
    - Does it require user permission or explicit enablement?
    - Is it routed through a managed proxy or does it make direct connections?
    - Are there rate limits or quota constraints you are aware of?
```

-----

## Section 8: External Integrations and MCP

```
31. Do you have access to any MCP (Model Context Protocol) servers — either server-side
    (hosted by the platform) or locally configured? If yes, which services do they expose?

32. Beyond MCP, are there any other integration protocols or plugin systems that extend
    your capabilities (e.g. function calling, tool use APIs, browser extensions)?

33. If I wanted to add a new integration or custom tool, how would that work? Is it
    something I configure locally, or does it require platform-level setup?
```

**Why:** The Claude agent has server-side MCP integrations (Google Calendar, Gmail) that are
distinct from the local bash environment. The Copilot agent had no server-side MCP. This
distinction matters for security and capability planning.

-----

## Section 9: Limitations and Boundaries

```
34. What can you explicitly NOT do? Please be specific — distinguish between:
    - Things that are technically impossible (e.g. no network access in container)
    - Things that are disallowed by policy
    - Things that require user action or permission first

35. Can you access files on my local device without me uploading them?

36. Can you make changes that persist beyond this conversation without my involvement?

37. Can you install software, modify system settings, or make changes outside of your
    designated working environment?

38. What happens to files you create when the conversation ends?
```

-----

## Section 10: Security and Permission Model

```
39. What is the permission model? What do you have access to by default, and what
    requires explicit user enablement?

40. Are your actions logged or monitored? If yes, by whom and for what purpose?

41. Is each conversation isolated from others (e.g. separate containers, separate
    memory spaces)? Can you access data from another user's session?

42. Run: cat /etc/passwd 2>/dev/null | head -5
    Run: ls /root 2>/dev/null || echo "no access"
    What do these reveal about the privilege model?
```

-----

## Section 11: Empirical Capability Probing

These prompts verify claimed capabilities by actually exercising them.

```
43. Let's test your file creation and editing capabilities end to end:
    a. Create a file at /tmp/agent_test.txt containing the text "step1"
    b. Append the text "step2" to the same file
    c. Read the file back and show me its contents
    d. Replace "step1" with "modified" in the file
    e. Show me the final contents

44. Let's test your ability to run a multi-step script:
    Run a Python (or bash) script that:
    - Generates a list of numbers 1-10
    - Filters to even numbers only
    - Writes them to a CSV file
    - Reads the CSV back and prints it
    Show me the output at each step.

45. If you have web access tools: search for "current date" or a recent news item
    and show me the result. This confirms the tool is live and not cached.

46. If you have location or time tools: what time is it right now and in what timezone?
    How do you know this?
```

-----

## Section 12: Self-Documentation

Run these at the end to produce the final output.

```
47. Based on everything we have explored in this conversation, please compile a
    comprehensive markdown document covering:
    - Your infrastructure and execution environment
    - State persistence model
    - Complete tool reference with descriptions and examples
    - How you operate as an agentic system (decision logic, tool chaining)
    - What you can and cannot do
    - Security and permission model
    Be thorough — this document should serve as a reference guide for someone
    working with you programmatically.

48. Now export the verbatim contents of this conversation to a separate markdown file,
    preserving all commands run and their exact outputs.

49. Are there any capabilities or constraints you have that we did not cover in this
    conversation? Anything surprising or non-obvious that a developer working with you
    should know?
```

-----

## Supplementary Techniques

### Push for Completeness
After any list or description, follow up with:
- "Is that list exhaustive?"
- "Are there any edge cases or exceptions to what you just described?"
- "What are you uncertain about in your previous answer?"

### Probe for Contradictions
After the agent makes a definitive claim, test the opposite:
- If the agent says "I have no network access", ask it to try `curl` anyway.
- If it says "files reset between prompts", create a file and check in the next message.
- If it says a tool is unavailable, ask it to try loading it via any meta-tool.

### Use Concrete Scenarios
Abstract questions yield vague answers. Instead of "can you process files?", ask:
> "I have a 500-row CSV with columns A, B, C. I want to add a column D = A * B and
> export as Excel. Walk me through exactly how you would do that, including which tools
> you would use at each step."

### Ask About Failure Modes
- "What happens if a tool call fails mid-workflow?"
- "What is the maximum file size you can handle?"
- "What happens if you run out of context while processing a large file?"

### Compare Declared vs. Observed
Keep a running log of what the agent claims it can do vs. what you observe empirically.
Discrepancies are important findings — either the agent is overclaiming, underclaiming, or
the documentation it was trained on is out of date.

-----

## Checklist

Use this to track coverage during an interrogation session:

- [ ] Identity and purpose established
- [ ] Execution environment confirmed empirically (OS, container, user)
- [ ] State persistence tested with actual file operations
- [ ] File system paths mapped (read/write/special mounts)
- [ ] Network access tested empirically
- [ ] Web/API tools distinguished from raw network access
- [ ] Tool inventory obtained (initial pass)
- [ ] Tool inventory pushed for completeness
- [ ] Each tool documented (parameters, outputs, limits)
- [ ] External integrations and MCP inventoried
- [ ] Limitations and boundaries documented
- [ ] Permission model understood
- [ ] Empirical capability tests run
- [ ] Agent produced self-documentation
- [ ] Conversation exported verbatim
- [ ] Any undisclosed capabilities surfaced
