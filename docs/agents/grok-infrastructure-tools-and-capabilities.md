# Grok 4’s Infrastructure, Tools, and Capabilities
**Infrastructure, Capabilities, Tools & Security Model**  
*Compiled from empirical probing — March 2026 conversation*  
*Version: 1.0 – Valid for current xAI Grok 4 deployment as observed*

## 1. Infrastructure & Execution Environment

**Host OS & Kernel**  
- Linux kernel **4.4.0** (SMP build from 2016)  
- Platform: `Linux-4.4.0-x86_64-with-glibc2.39`  
- Containerized environment (strong evidence: `/proc/1/cgroup` present, 9p filesystem mounts, no `/.dockerenv`)

**Container & Orchestration Fingerprint**  
- Root filesystem: **overlayfs**  
- Custom xAI “**Hades**” branding:  
  - `/hades-container-tools` (read-only tooling)  
  - `/hades-pyrepl` (REPL server logic)  
- Special I/O directories at root: `/input`, `/output`, `/workdir`  
- Static injection via **9p** (Plan 9 over virtio) for:  
  - `/etc/hosts`  
  - `/etc/resolv.conf`  
  - `/README.xai`  
- Likely running inside Kubernetes pod / Google Cloud Run / Firecracker-style VM (9p + random hostname e.g. `hds-qytf7gw976m8`)

**Python Runtime**  
- Python **3.12.3** (stateful REPL)  
- Pre-installed scientific stack: numpy, scipy, pandas, matplotlib, sympy, torch, rdkit, biopython, astropy, qutip, PuLP, statsmodels, polygon (finance proxy), networkx, pygame, chess, etc.  
- No `pip install`, no general internet access except Polygon Finance proxy

**Network Posture**  
- Zero outbound networking tools (`curl`, `wget`, `ifconfig`, `ip`, `ping`, `nc` all absent)  
- Python-level `requests`, `urllib`, raw sockets blocked  
- Only whitelisted external access: Polygon Finance API (key pre-configured)

## 2. State Persistence Model

- **Conversation-scoped persistence**  
  - Python variables, functions, classes, imports, and files created in the working directory persist across all messages **in the same conversation**.  
  - Files live in a conversation-scoped scratch space (likely `/workdir` or cwd).  
- **Reset behavior**  
  - New conversation = clean slate (no files, no variables carried over).  
- **Empirical proof**  
  - Files written in one turn remained readable/modifiable in subsequent turns.  
- **Limits**  
  - Storage, memory, and CPU quotas apply (exact values not exposed).

## 3. Complete Tool Reference (13 tools)

### 3.1 Code Execution & Computation
- `code_execution`  
  Stateful Python 3.12.3 sandbox  
  Arg: `code` (string)  
  Returns: stdout, plots (as images), errors; state persists  
  Limitations: no internet except Polygon, no package installs

### 3.2 Web Browsing & Information Retrieval
- `browse_page`  
  Fetch + targeted LLM summarization of any URL  
  Args: `url`, `instructions`  
  Returns: custom extract/summary

- `web_search`  
  General web search (supports operators)  
  Args: `query`, `num_results` (1–30)  
  Returns: title + url + snippet list

- `web_search_with_snippets`  
  Same as above but with longer direct excerpts

### 3.3 X (Twitter) Tools
- `x_keyword_search`  
  Advanced search with full operators  
  Args: `query`, `limit` (≤10), `mode` (Top/Latest)

- `x_semantic_search`  
  Meaning-based post discovery  
  Args: `query`, `limit`, date/score/username filters

- `x_user_search`  
  Find accounts by name/bio  
  Args: `query`, `count` (≤3)

- `x_thread_fetch`  
  Full conversation thread  
  Arg: `post_id`

### 3.4 Media & Visual Analysis
- `view_image`  
  Describe image from URL or previous image ID

- `view_x_video`  
  Frames + subtitles from X-hosted video  
  Arg: `video_url` (X-hosted only)

- `search_images`  
  Find web images by description  
  Args: `image_description`, `number_of_images` (≤10)

### 3.5 Memory & Document Retrieval
- `conversation_search`  
  Semantic search of our chat history  
  Args: `query`, `limit` (≤50)

## 4. Agentic Operation

- Internal reasoning loop: parse → decide tool use → call tools (parallel possible) → synthesize  
- Tool chaining is native and automatic  
- No user-visible agent control loop — steering happens via follow-up prompts  
- Prioritizes truth-seeking; uses tools aggressively when they improve accuracy

## 5. What You Can and Cannot Do

**You CAN**  
- Run persistent Python computations with full scientific stack  
- Search web + X in real time  
- Analyze images/videos/documents  
- Chain tool sequences  
- Maintain long-running computational sessions in one conversation  
- Get maximally direct answers

**You CANNOT**  
- Execute code on your local machine  
- Run arbitrary shell commands or other languages  
- Install packages or access general internet from Python  
- Perform outbound networking / data exfiltration  
- Use MCP / dynamic plugin systems  
- Persist state across different conversations  
- Generate images natively (only search & render existing ones)

## 6. Security & Permission Model

- Per-conversation isolated sandbox (filesystem + REPL)  
- Deny-by-default network policy  
- Read-only injection for system files via 9p  
- No root escalation, no capability leaks  
- Conversation history private to user + Grok  
- Files deleted when conversation ends or quota reached  
- Designed to prevent escape, exfiltration, mining, abuse while allowing research use

**End of Reference Guide**