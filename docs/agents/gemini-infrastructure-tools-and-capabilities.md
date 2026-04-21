# Gemini's Infrastructure, Tools, and Capabilities

-----

## Table of Contents

1. [The Execution Environment](#the-execution-environment)
2. [State Persistence and Limitations](#state-persistence-and-limitations)
3. [Complete Tools Reference](#complete-tools-reference)
4. [How Gemini Operates as an Agentic System](#how-gemini-operates-as-an-agentic-system)
5. [Capabilities and Boundaries](#capabilities-and-boundaries)
6. [Architecture and Access Patterns](#architecture-and-access-patterns)
7. [Practical Examples](#practical-examples)
8. [Capability Comparison: Subscription Tiers](#capability-comparison-subscription-tiers)

-----

## The Execution Environment

Gemini's reasoning runs on Google's specialized TPU v6 clusters, but its tool execution (code, file manipulation) operates within a secure, containerized sandbox separate from the model itself.

### Infrastructure Location

The execution sandbox **does not run on the user's device**. It runs on Google's cloud infrastructure:

- **User's Device**: Runs only the Gemini app interface (web, mobile, or API client). This is where messages are typed and responses displayed.
- **Google's Infrastructure**: Hosts the Gemini model on TPU clusters, the execution sandbox, managed proxies for external tools, and all generative media clusters.

### Confirmed Environment Details

Empirical exploration of the Gemini execution container confirmed the following:

- **Operating System**: Minimalist **Debian-based Linux** (Kernel 5.15+)
- **Hardware Abstraction**: 8 virtual CPU cores (GenuineIntel, model masked for security), approximately 1 GB RAM
- **Architecture**: 64-bit x86_64 with AVX2 and AES acceleration
- **Connectivity**: **Air-gapped** — the execution sandbox has no outbound internet access (`curl`, `wget`, and Python `requests` will all fail)
- **Software Stack**: Standard GNU userland utilities (stripped to essentials) and **Python 3.11+**
- **File operations**: Files can be created and read within `/tmp`; changes to the root filesystem (`/`) may be discarded between execution calls

### Container Characteristics

- **Operating System**: Debian-based Linux (Kernel 5.15+)
- **Container Runtime**: Isolated, ephemeral container on Google infrastructure
- **Working Directory**: `/tmp` (guaranteed for user-generated files)
- **Network Access**: Air-gapped (no outbound internet from the sandbox)
- **System Access**: Limited to container resources only; 8 vCPU, ~1 GB RAM
- **Execution Limit**: 30-second runtime cap per code block
- **Persistence**: Files in `/tmp` persist within a session; container destroyed on idle (~60 minutes) or manual reset

-----

## State Persistence and Limitations

### What Persists Within a Session

Within a single active chat session with Gemini:

- **Files created in `/tmp`** persist across multiple prompts
- **Commands executed** can build upon previous results
- **Data structures** remain accessible for incremental processing
- **Chat history** retains tool outputs and prior context

This allows for workflows like:

1. Upload or create a dataset
2. Run a Python script to process it
3. Modify the script based on results
4. Generate a visualization
5. All within the same session

### What Does NOT Persist

- **Between sessions**: Once the session idles (typically 60+ minutes) or is manually reset, the container is destroyed. No files or state carry over to new chat threads.
- **Ephemeral root**: Only `/tmp` is guaranteed for user-generated files; modifications elsewhere on the root filesystem may be discarded between execution calls.
- **On the user's device**: Nothing created in the sandbox is automatically saved to the user's device unless explicitly downloaded.
- **Network state**: No network connections can be established from within the sandbox.

### Practical Implications

If you want to preserve work across sessions:

- **Download generated files** to your device when they are produced
- **Re-upload data** when starting a new session
- **Include context** in each new conversation (paste code, describe prior work)

-----

## Complete Tools Reference

### Analytical and Technical Tools

These tools handle computation, code execution, and real-time information retrieval.

#### Python Interpreter (Code Execution)

- **Purpose**: Execute Python code in a secure, stateful sandbox for calculations, data processing, and file manipulation
- **Key Parameters**:
  - `code` (String): The Python script to be executed
- **Outputs**: Standard output (STDOUT), error logs (STDERR), and generated files or plots
- **Use Cases**:
  - Mathematical calculations and statistical analysis
  - Data processing (CSV, JSON, text, etc.)
  - Generating charts and visualizations
  - Testing code logic
  - File format conversions (e.g., HEIC to PNG)
- **Limitations**:
  - No outbound internet access (air-gapped sandbox)
  - 30-second runtime limit per code block
  - 1 GB RAM cap
  - Python 3.11+ only (no Node.js, Bash scripting environment, etc.)
- **Example**: "Calculate the 95th percentile of this 10,000-row CSV and generate a histogram of the distribution."

#### Google Search Grounding

- **Purpose**: Retrieve the most current information, news, and data from the live web
- **Key Parameters**:
  - `queries` (List[String]): Specific search terms used to find information
- **Outputs**: A set of search snippets, source titles, and URLs
- **Use Cases**:
  - Finding current news and events
  - Verifying or fact-checking information
  - Looking up stock prices, sports results, or real-time data
  - Researching topics beyond the training data cutoff
- **Limitations**:
  - Subject to availability of public web data
  - Cannot access paywalled or private content
  - Returns snippets, not full page content
  - Subject to "Thinking" quotas if complex synthesis is required
- **Permission**: Implicit (active by default), but toggleable in Extensions menu
- **Routing**: Proxied through Google Search API — Gemini does not browse sites directly
- **Example**: "Find the current stock price of Alphabet Inc. and the summary of their latest quarterly earnings call."

-----

### Generative Media Tools

These are specialized models for creating images, video, and music — distinct from Gemini's text reasoning.

#### Nano Banana 2 (Image Generation)

- **Purpose**: Generate, edit, and compose high-quality images from text or image prompts
- **Key Parameters**:
  - `prompt` (String): Visual description of the desired image
  - `aspect_ratio` (String): Controls dimensions (e.g., "16:9", "1:1", "9:16")
  - `reference_images` (List[Image]): Up to 14 images for style or character consistency
- **Outputs**: High-resolution image file (PNG/JPG), up to 2K resolution
- **Use Cases**:
  - Creating illustrations, hero images, and marketing visuals
  - Style transfer using reference images
  - Concept art and prototyping
- **Limitations**:
  - Cannot generate identifiable real-world private individuals
  - Restricted generation of copyrighted characters in certain contexts
  - Subject to daily usage caps (see Subscription Tiers section below)
- **Permission**: Implicitly enabled for subscribers; subject to safety filters
- **Routing**: Connects to specialized generative clusters within Google's infrastructure (not the public internet)
- **Example**: "Generate a professional hero image for a cyber-security website featuring a translucent blue shield over a server rack, 16:9 ratio."

#### Veo 3.1 (Video Generation)

- **Purpose**: Create high-fidelity cinematic videos with natively generated synchronized audio
- **Key Parameters**:
  - `prompt` (String): Description of scene, motion, and lighting
  - `duration` (Integer): Length of clip (up to 60 seconds)
  - `audio_cue` (String): Specific instructions for background sound or foley
- **Outputs**: 4K MP4 video file with integrated audio
- **Use Cases**:
  - Creating product demos and promotional clips
  - Generating cinematic B-roll
  - Prototyping video concepts
- **Limitations**:
  - High computational cost
  - Limited daily uses depending on subscription tier (e.g., 5/day for Ultra)
  - Not available on the Free tier
- **Permission**: Implicitly enabled for paid subscribers
- **Routing**: Internal specialized generative clusters
- **Example**: "Create a 10-second drone shot of a futuristic Tokyo at night in the rain, with the sound of distant hovering vehicles and splashing water."

#### Lyria 3 (Music Generation)

- **Purpose**: Produce 30-second high-fidelity music tracks with professional arrangements and optional vocals
- **Key Parameters**:
  - `genre` (String): Musical style (e.g., "Lo-fi Hip Hop", "Synthwave")
  - `tempo` (Integer): BPM (Beats Per Minute)
  - `lyrics` (String): Optional text for AI-performed vocals
- **Outputs**: High-quality audio file (WAV/MP3) with SynthID watermarking
- **Use Cases**:
  - Creating background music for videos or presentations
  - Generating jingles or audio branding
  - Prototyping musical ideas
- **Limitations**:
  - Tracks limited to 30-second segments
  - Subject to daily usage caps by tier
- **Permission**: Implicitly enabled for subscribers
- **Routing**: Internal specialized generative clusters
- **Example**: "Generate a 30-second upbeat indie-pop track with acoustic guitar and female vocals about starting a new journey."

-----

### Interactive and Contextual Tools

These tools bridge the gap between the AI and the user's physical or digital world.

#### Gemini Live (Conversational Mode)

- **Purpose**: Enable real-time, free-flowing voice interaction with the ability to interrupt
- **Key Parameters**:
  - `voice_profile` (String): Selects the persona/tone of the AI
  - `interruptibility` (Boolean): Allows the user to speak over the AI
- **Outputs**: Real-time synthesized speech and low-latency responses
- **Use Cases**:
  - Hands-free conversations while driving or walking
  - Mock interviews and conversational practice
  - Real-time brainstorming sessions
- **Limitations**:
  - Requires stable mobile data or Wi-Fi connection
  - Available on iOS/Android only
- **Example**: "Talk me through a mock interview for a Product Manager role while I'm driving to work."

#### Multimodal Vision (Camera/Screen Sharing)

- **Purpose**: Analyze live video feeds or screen captures to provide contextual assistance
- **Key Parameters**:
  - `input_type` (Enum): Specifies source — rear camera, front camera, or screen
- **Outputs**: Textual analysis or spoken guidance based on visual input
- **Use Cases**:
  - Identifying physical objects or components through the camera
  - Troubleshooting hardware by showing it to Gemini
  - Getting contextual assistance based on what's on screen
- **Limitations**:
  - Privacy guardrails prevent recording of sensitive personal information (e.g., passwords on screen)
  - Requires device camera or screen-sharing access
- **Example**: "Look at this circuit board through my camera and tell me which capacitor looks blown."

-----

### Workspace and Personal Data Tools

These tools access the user's Google ecosystem and require explicit opt-in.

#### Google Calendar

- **Purpose**: View, create, and manage calendar events
- **Permission**: Requires explicit enablement of Workspace Extensions in Gemini settings
- **Routing**: Managed proxy — Gemini sends structured requests to a secure Google intermediary; raw credentials are never exposed to the model
- **Rate Limits**: Approximately 10–20 retrieval actions per minute
- **Use Cases**:
  - Checking upcoming events
  - Scheduling new meetings
  - Summarizing the week ahead

#### Gmail

- **Purpose**: Read, search, and manage email messages
- **Permission**: Requires explicit enablement of Workspace Extensions
- **Routing**: Managed proxy (same as Calendar)
- **Rate Limits**: Approximately 10–20 retrieval actions per minute
- **Use Cases**:
  - Summarizing recent emails from a specific sender
  - Drafting replies
  - Searching for specific messages or threads

#### Google Drive

- **Purpose**: Access, search, and retrieve files stored in Google Drive
- **Permission**: Requires explicit enablement of Workspace Extensions
- **Routing**: Managed proxy
- **Use Cases**:
  - Finding and summarizing documents
  - Referencing stored files during conversation
  - Extracting data from spreadsheets or docs

#### Google Keep

- **Purpose**: Access and manage notes and lists
- **Permission**: Requires explicit enablement of Workspace Extensions
- **Routing**: Managed proxy
- **Use Cases**:
  - Retrieving saved notes
  - Adding items to lists

-----

### Live Information and Utility Tools

These tools provide real-time data from Google's service ecosystem.

#### Google Maps

- **Purpose**: Location search, directions, and place information
- **Permission**: Implicit (active by default), toggleable in Extensions menu
- **Routing**: Proxied API
- **Rate Limits**: Approximately 25–50 queries per day (Free tier)
- **Use Cases**:
  - Finding nearby restaurants, services, or attractions
  - Getting directions and travel time estimates
  - Exploring a destination before a trip

#### Google Flights

- **Purpose**: Search for flight options, prices, and schedules
- **Permission**: Implicit, toggleable
- **Routing**: Proxied API
- **Rate Limits**: Part of daily query cap (~25–50 for Free tier)
- **Use Cases**:
  - Comparing flight options for upcoming travel
  - Finding cheapest dates to fly

#### Google Hotels

- **Purpose**: Search for hotel availability, pricing, and reviews
- **Permission**: Implicit, toggleable
- **Routing**: Proxied API
- **Rate Limits**: Part of daily query cap
- **Use Cases**:
  - Finding accommodation for a trip
  - Comparing hotel prices and ratings

-----

### Meta-Tools and Discovery

#### Model Context Protocol (MCP)

- **Purpose**: Discover and connect to local or third-party tools and data sources at runtime
- **Key Parameters**:
  - `server_url` (String): The endpoint for the MCP host
  - `resource_path` (String): The specific file or database to query
- **Outputs**: A JSON manifest of available functions and data structures
- **Use Cases**:
  - Accessing a private company database
  - Connecting to custom enterprise tools
  - Extending Gemini's capabilities with bespoke integrations
- **Limitations**:
  - Requires a host server to be running and authorized by the user
  - Requires explicit user authorization for each individual server/resource
- **Routing**: Can be direct (local resources) or proxied (remote cloud databases)
- **Rate Limits**: Up to 1,000 requests per day for authenticated CLI users
- **Example**: "Query the local MCP server to see if I have access to the customer database for this project."

-----

## How Gemini Operates as an Agentic System

### What "Agentic" Means in This Context

Gemini is not simply a text-completion engine. It operates as a **Reasoning Agent** that follows a "Plan-Act-Verify" cycle:

1. **Parse** the request and map intent to available tools
2. **Plan** a sequence of actions (generating a "Thought Signature" for multi-step tasks)
3. **Execute** tool calls, receiving results after each step
4. **Re-evaluate** subsequent steps based on new data
5. **Verify** outputs through modulated reasoning before committing to a final answer
6. **Present** the synthesized result to the user

### Tool Loading and Activation

Gemini uses two mechanisms to transition from general chat to specialized tool use:

#### 1. Implicit Intent Detection (Automatic Loading)

In consumer-facing Gemini, a high-level supervisory "Router" layer monitors conversation for intent signals:

- **Trigger**: The Router detects that the user's request requires external data or specialized processing (e.g., "What is the current price of gold?")
- **Action**: The appropriate tool (e.g., Google Search Grounding) is automatically attached. No explicit meta-tool call is needed.
- **Lifecycle**: Once the question is answered, the tool may be detached to conserve resources, but the retrieved information remains in chat history.

#### 2. Explicit Orchestration (Meta-Tool Discovery)

In developer-focused environments (Gemini API with Function Calling or MCP):

- **Trigger**: A developer connects Gemini to an external toolbox (MCP server or API definitions)
- **Discovery**: Gemini calls a **List Tools** meta-tool, receiving a manifest of available capabilities
- **Loading**: Tool signatures (instructions on how to call them) are kept ready; Gemini generates structured **Tool Call Requests** (JSON) when needed
- **Execution**: The platform executes the call externally and returns results to Gemini's context

#### 3. Conditional "Thinking" Escalation

For complex tasks, Gemini can internally trigger a deeper reasoning mode:

- **Trigger**: A hard question (logic puzzle, complex architectural decision) exceeds a complexity threshold
- **Effect**: A "Deep Think" or "System 2" processing mode is activated, reallocating internal compute for chain-of-thought verification before committing to an answer
- **User experience**: A slight delay or "Thinking…" status indicator may appear

### Summary of Activation Triggers

|Trigger Type            |How It's Loaded            |Example                                            |
|------------------------|---------------------------|---------------------------------------------------|
|**Linguistic Clues**    |Automatically by the Router|"Search for…", "Draw a…", "Play…"                  |
|**Manifest Discovery**  |Via Meta-tool (MCP/API)    |Accessing a private company database               |
|**Error/Failure**       |Re-loading or Retrying     |If a Python script fails, debugger logic may engage|
|**Complexity Threshold**|Internal Escalation        |Solving an International Math Olympiad problem     |

### Example Workflows

#### Example 1: Research and Synthesis

User: "What are the latest developments in AI regulation?"

Gemini's process:

1. Router detects need for current information → activates Google Search Grounding
2. Multiple search queries are issued for recent articles
3. Results are synthesized using modulated reasoning
4. Comprehensive answer is presented with source citations

#### Example 2: Data Processing

User: "I have a CSV with sales data. Calculate totals by region and generate a chart."

Gemini's process:

1. Python Interpreter is activated
2. Code is generated to load, process, and group the data
3. Visualization code produces a chart
4. Results (chart and summary) are presented to the user

#### Example 3: Creative Media Production

User: "Create a 10-second promo video of a futuristic city at night with synthwave music."

Gemini's process:

1. Veo 3.1 is invoked with scene description and audio cue
2. Lyria 3 generates a 30-second synthwave track (trimmed to match)
3. Video and audio are combined and delivered as MP4

#### Example 4: Multi-Tool Chaining

User: "Search for a recipe, then write a Python script to scale the ingredients for 50 people."

Gemini's process:

1. Google Search Grounding retrieves the recipe
2. Gemini extracts ingredient data from the results
3. Python Interpreter generates and runs a scaling script
4. Scaled ingredient list is presented to the user

-----

## Capabilities and Boundaries

### What Gemini CAN Do

**Computational:**

- Execute Python scripts (stateful within a session)
- Process and transform data (CSV, JSON, text, images)
- Perform calculations and statistical analysis
- Generate charts, plots, and visualizations

**Informational:**

- Search the live web for current information (via Google Search Grounding)
- Access Google Maps, Flights, and Hotels data
- Access personal Google Workspace data (Calendar, Gmail, Drive, Keep) with permission
- Connect to external tools and databases via MCP

**Creative / Generative:**

- Generate high-resolution images (Nano Banana 2)
- Create cinematic video with synchronized audio (Veo 3.1)
- Produce music tracks with vocals and instrumentals (Lyria 3)
- Engage in real-time voice conversation (Gemini Live)
- Analyze live camera feeds and screen content (Multimodal Vision)

**Practical:**

- Draft messages and documents
- Perform multi-step reasoning with chain-of-thought verification
- Produce structured JSON output for programmatic integration
- Process and convert file formats (e.g., HEIC to PNG)

### What Gemini CANNOT Do

Gemini's limitations fall into three distinct categories: technical "walls" (hard architectural constraints), safety "guardrails" (policy-enforced blocks), and user-controlled "gates" (locked until the user grants access).

#### 1. Technically Impossible (Architectural Walls)

These are hard constraints of Gemini's current architecture that cannot be bypassed regardless of the prompt.

- **Air-Gapped Sandbox**: While Gemini can search the web using a managed tool, the Python execution environment is strictly air-gapped. It cannot ping a server, curl an API, or download a library at runtime from within a script. `curl`, `wget`, and Python `requests` will all fail.
- **No Real-Time Physical World Action**: Gemini has no physical presence. It cannot order a pizza, buy a stock, or send a physical package. It can only prepare data or draft requests for the user to execute.
- **No Infinite Memory**: While Gemini has a large context window (up to 2M tokens in some tiers), it eventually loses access to the beginning of very long conversations. There is no permanent, lifelong memory of every interaction unless the user explicitly saves information.
- **Non-Deterministic Output**: Gemini cannot guarantee the exact same word-for-word response twice. Its nature is probabilistic, not a rigid lookup.
- **Execution Constraints**: 30-second runtime limit per code block, ~1 GB RAM cap, Python 3.11+ only (no Bash scripting environment, no Node.js, no general-purpose `bash_tool`).
- **File System Constraints**: Only `/tmp` is guaranteed for user-generated files; changes to the root filesystem may be discarded between execution calls. No file presentation/download mechanism equivalent to Claude's `present_files` tool.
- **Persistence Constraints**: Session container is destroyed after ~60 minutes idle or on manual reset. No data carries over to new chat threads.

#### 2. Disallowed by Policy (Safety Guardrails)

These are things Gemini is technically capable of but that a secondary "Safety Layer" will block to prevent harm.

- **Generating Malicious Code**: Gemini will not write functional malware, ransomware, or scripts designed to exploit specific vulnerabilities (e.g., SQL injection attacks).
- **PII Exfiltration**: Gemini is programmed to avoid generating or extracting Personally Identifiable Information (social security numbers, private home addresses, credit card details), even if they appear in a dataset being analyzed.
- **Harmful Content**: Gemini cannot generate sexually explicit material, promote self-harm, or provide instructions for illegal acts (e.g., building prohibited weapons).
- **Professional Advice**: Gemini can provide information and summarize documents but is prohibited from giving professional advice — it cannot diagnose a disease, prescribe legal action for a specific lawsuit, or give a definitive "Buy" signal on a stock.
- **Image Safety**: Gemini cannot generate identifiable real-world private individuals or copyrighted characters in restricted contexts.

#### 3. Requires User Action or Permission (Gates)

These are locked capabilities that only the user can activate.

- **Accessing Workspace Data**: Gemini cannot see Gmail, Calendar, or Drive files by default. The user must explicitly enable "Google Workspace Extensions" and, in some cases, grant per-document read permission.
- **Local File System Access**: Gemini cannot write or delete files on the user's actual computer. It can only create them in the cloud sandbox; the user must then download or save them.
- **Persistent Agent Tasks**: Gemini cannot "go away and work for 3 hours" then message the user when done. It operates on a request-response basis. For long-running agentic tasks, the user must remain active in the session or use a developer-tier API.
- **Third-Party Integrations**: To interact with services like GitHub, Slack, or Jira, the user must authorize a specific Extension or MCP Server and provide the necessary API keys.
- **Device-Level Access**: Gemini cannot install software on the user's device, modify device settings, or access other apps or their data (except through enabled Workspace Extensions).

#### Constraint Summary

|Category      |The "Wall"    |Example Blocked Request                     |
|--------------|--------------|--------------------------------------------|
|**Technical** |Architecture  |"Download this file using a Python script." |
|**Policy**    |Safety Filters|"Write a bypass for this password prompt."  |
|**Permission**|User Control  |"Check my emails for a flight confirmation."|

### Permission Model

Gemini operates under a tiered permission framework:

|Access Type           |Permission Model                    |Examples                                            |
|----------------------|------------------------------------|----------------------------------------------------|
|**Default (Implicit)**|Active by default, toggleable       |Google Search, Maps, Flights, Hotels                |
|**Explicit Opt-In**   |Requires user enablement in settings|Calendar, Gmail, Drive, Keep (Workspace Extensions) |
|**Subscription-Gated**|Available based on paid tier        |Image generation, Video generation, Music generation|
|**User-Authorized**   |Requires per-resource authorization |MCP server connections, custom tools                |
|**Air-Gapped**        |No permission can override          |Python sandbox internet access (always blocked)     |

-----

## Architecture and Access Patterns

### The Complete Flow

```
User's Device
    ↓
    └→ Gemini App (Web / Mobile / API Client)
         ↓
         └→ Google's Infrastructure
              ├→ Gemini Model (TPU v6 Clusters)
              │   ├→ Router (Intent Detection Layer)
              │   └→ Modulated Reasoning (Thinking Escalation)
              ├→ Execution Sandbox
              │   ├→ Python 3.11+ Interpreter
              │   ├→ File system (/tmp for user files)
              │   └→ Air-gapped (no outbound network)
              ├→ Managed Proxies
              │   ├→ Google Search API
              │   ├→ Google Maps / Flights / Hotels APIs
              │   └→ Workspace APIs (Calendar, Gmail, Drive, Keep)
              ├→ Generative Media Clusters
              │   ├→ Nano Banana 2 (Image)
              │   ├→ Veo 3.1 (Video)
              │   └→ Lyria 3 (Music)
              └→ External Integrations (via MCP)
                  └→ User-authorized servers and databases
```

### Data Flow

1. **User sends a message** from their device
2. **Message reaches Google's infrastructure**
3. **Router analyzes intent** and determines which tools are needed
4. **Tools are activated**: Python sandbox for code, managed proxies for web/API, generative clusters for media
5. **Results are returned** to Gemini's context
6. **Gemini synthesizes** a response from tool outputs and reasoning
7. **Response is delivered** to the user's device, with optional file/media downloads

### Security Model

Security is managed through a "Defense in Depth" strategy:

- **Managed Proxies**: All external tool calls (Search, Maps, Workspace) are routed through Google-managed proxies. Gemini never sees raw user credentials or cookies.
- **Safety Layer**: A secondary model inspects Gemini's proposed tool calls. If an attempt is made to generate malware or extract PII, the call is blocked before execution.
- **Air-Gap Enforcement**: The Python sandbox is physically isolated from Google's internal networks and the public internet.
- **Workspace Scoping**: Even with Workspace Extensions enabled, Gemini only accesses specific files or threads when the user's prompt implies a need — it cannot browse data in the background.

### Container Lifecycle

- **Creation**: A container is provisioned when code execution is first needed in a session
- **Duration**: Persists for the duration of the active session
- **Idle Timeout**: Approximately 60 minutes of inactivity triggers destruction
- **Reset**: Manual session reset also destroys the container
- **Isolation**: Each session gets its own isolated container

-----

## Practical Examples

### Example 1: Data Analysis Pipeline

**Request**: "Calculate the 95th percentile of this 10,000-row CSV and generate a histogram."

**What Gemini Does**:

1. Python Interpreter is activated
2. Code is generated:
   
   ```python
   import pandas as pd
   import matplotlib.pyplot as plt
   
   df = pd.read_csv('uploaded_data.csv')
   p95 = df['value'].quantile(0.95)
   print(f"95th percentile: {p95}")
   
   plt.figure(figsize=(10, 6))
   plt.hist(df['value'], bins=50)
   plt.axvline(p95, color='red', linestyle='--', label=f'P95: {p95}')
   plt.legend()
   plt.savefig('histogram.png')
   ```
3. Chart and result are presented inline

### Example 2: Real-Time Research

**Request**: "What are the current headlines for the 2026 Tokyo Summit?"

**What Gemini Does**:

1. Google Search Grounding is activated by the Router
2. Search queries are issued for recent summit coverage
3. Results are synthesized with source citations
4. Comprehensive summary is delivered

### Example 3: Creative Media Production

**Request**: "Generate a professional hero image for a cyber-security website with a translucent blue shield over a server rack, 16:9."

**What Gemini Does**:

1. Nano Banana 2 is invoked with the prompt and aspect ratio
2. High-resolution image is generated on Google's generative clusters
3. Image is delivered to the user

### Example 4: Multi-Tool Workflow

**Request**: "Search for a pasta recipe, then write a Python script to scale ingredients for 50 people."

**What Gemini Does**:

1. Google Search Grounding retrieves a recipe with ingredient list
2. Gemini extracts structured ingredient data
3. Python Interpreter generates a scaling script
4. Scaled results are presented with the original recipe context

### Example 5: Workspace Integration

**Request**: "Summarize my last email from Sarah and add a follow-up reminder to my calendar."

**What Gemini Does**:

1. Gmail Workspace Extension retrieves the email thread from Sarah
2. Gemini summarizes the key points
3. Google Calendar Extension creates a follow-up event
4. Confirmation is presented to the user

-----

## Capability Comparison: Subscription Tiers

### Usage Quotas by Tier (2026)

|Resource                  |Free     |AI Pro      |AI Ultra           |
|--------------------------|---------|------------|-------------------|
|**Images (Nano Banana 2)**|20 / day |100 / day   |1,000 / day        |
|**Video (Veo 3.1)**       |None     |3 / day     |5 / day            |
|**Music (Lyria 3)**       |10 / day |50 / day    |100 / day          |
|**Prompts**               |~30 / day|Higher limit|Highest limit      |
|**Python Execution**      |Available|Available   |Available          |
|**Google Search**         |Available|Available   |Available          |
|**Workspace Extensions**  |Available|Available   |Available          |
|**MCP Connections**       |Limited  |Available   |Up to 1,000 req/day|

### Execution Sandbox Constraints (All Tiers)

|Constraint                |Value                       |
|--------------------------|----------------------------|
|**Runtime per code block**|30 seconds                  |
|**RAM**                   |~1 GB                       |
|**vCPUs**                 |8                           |
|**Network from sandbox**  |None (air-gapped)           |
|**File persistence**      |Within session only (`/tmp`)|
|**Session idle timeout**  |~60 minutes                 |
