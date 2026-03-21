# Claude’s Infrastructure, Tools, and Capabilities

-----

## Table of Contents

1. [The Ubuntu Environment](#the-ubuntu-environment)
2. [State Persistence and Limitations](#state-persistence-and-limitations)
3. [Complete Tools Reference](#complete-tools-reference)
4. [How Claude Operates as an Agentic System](#how-claude-operates-as-an-agentic-system)
5. [Capabilities and Boundaries](#capabilities-and-boundaries)
6. [Architecture and Access Patterns](#architecture-and-access-patterns)
7. [Practical Examples](#practical-examples)
8. [Capability Comparison: Mobile vs. Windows Desktop PC](#capability-comparison-mobile-vs-windows-desktop-pc)

-----

## The Ubuntu Environment

When you interact with Claude from your device, you’re connecting to Anthropic’s servers. Claude runs as a containerized application with access to a Linux environment and a defined set of tools. The key insight is that Claude can take autonomous actions within defined constraints—it can execute commands, fetch information, create files, and manage data on your behalf.

### Infrastructure Location

The Ubuntu 24 environment where Claude can execute commands **does not run on your device**. Instead, it runs on **Anthropic’s servers**. Here’s the architecture:

- **Your Device**: Runs only the Claude app interface (frontend). This is where you type messages and see responses.
- **Anthropic’s Infrastructure**: Hosts the Claude AI model, the Ubuntu container, and all computational resources.

### Confirmed Environment Details

Direct exploration of the container has verified the following:

- **Directory structure**: Standard Linux layout — `/usr`, `/var`, `/etc`, `/tmp`, `/home`, `/dev`, `/proc`, `/sys`, `/run`, `/mnt` — confirming a genuine Ubuntu environment
- **Container runtime**: A `.dockerenv` file at the filesystem root confirms the environment runs in a Docker container
- **API communication**: A `process_api` executable (~3.2 MB) at the root handles communication between the container and the Claude model
- **Container metadata**: A `container_info.json` file at the root records each container's unique name and Unix creation timestamp, confirming containers are freshly instantiated per conversation session
- **File operations**: Files can be created, written, and read anywhere within writable paths such as `/usr/local/` and `/home/claude/`
- **Network tools**: Standard networking utilities (`ifconfig`, `ip addr`) are not installed, consistent with the disabled network access policy

### Container Characteristics

- **Operating System**: Ubuntu 24
- **Container Runtime**: Docker
- **Working Directory**: `/home/claude` (for temporary work)
- **Network Access**: Disabled (as per system configuration)
- **Special Mounts**: `/mnt/user-data/` (for file uploads and outputs), `/mnt/skills/` (for instruction skills)
- **System Access**: Limited to container resources only
- **Persistence**: Files persist within a conversation; containers reset between conversations

-----

## State Persistence and Limitations

### What Persists Within a Conversation

When you’re in a single conversation thread with Claude:

- **Files created by Claude** persist in the Ubuntu environment
- **Commands executed** can build upon previous commands
- **Data structures** in memory persist across prompts
- **Working files** remain accessible in subsequent messages

This allows for workflows like:

1. Create a Python script
2. Execute it
3. Modify it based on results
4. Run it again
5. All within the same conversation

### What Does NOT Persist

- **Between conversations**: Each new conversation spins up a fresh Docker container with a unique name and creation timestamp (recorded in `/container_info.json`). The previous container and all its contents are destroyed.
- **Between devices/browsers**: Each Claude session may run in its own isolated container instance.
- **On your device**: Nothing created in the container is automatically saved to your device unless you explicitly download it via the `present_files` tool.
- **Network state**: No network connections can be maintained; external services are not reachable from within the container.

### Practical Implications

If you want to preserve work across conversations:

- **Download files** to your device using the file presentation tool
- **Upload them back** when you want to continue working on them
- **Include context** in each new conversation (paste code, describe what you’re working on)

-----

## Complete Tools Reference

### Tier 1: Core File and Computation Tools

These tools are always available and don’t require loading.

#### `bash_tool`

- **Purpose**: Execute bash commands in the Ubuntu container
- **Use Cases**:
  - Running scripts (Python, JavaScript, shell scripts, etc.)
  - Processing files (text manipulation, format conversion, etc.)
  - System administration tasks
  - Testing code logic
  - Installing packages via pip or npm
- **Limitations**:
  - No network access (can’t download from internet)
  - Limited to container resources
  - Commands execute as unprivileged user
- **Example**:
  
  ```bash
  python3 script.py
  npm install lodash
  cat file.txt
  ```

#### `create_file`

- **Purpose**: Create new files in the Ubuntu container
- **Parameters**:
  - `path` - Where to create the file (typically `/home/claude/` or `/mnt/user-data/outputs/`)
  - `file_text` - The content to write
  - `description` - Why you’re creating this file
- **Use Cases**:
  - Writing code files (Python, JavaScript, etc.)
  - Creating configuration files
  - Generating documents
  - Building artifacts for download
- **Example**: Creating a Python script, HTML page, JSON config, etc.

#### `str_replace`

- **Purpose**: Edit existing files by replacing specific text
- **Parameters**:
  - `path` - File to edit
  - `old_str` - Exact text to find (must appear exactly once)
  - `new_str` - Text to replace it with (can be empty to delete)
  - `description` - Why you’re making this edit
- **Limitations**: The old_str must match exactly (no regex, no partial matches)
- **Use Cases**:
  - Modifying code files
  - Updating configuration files
  - Fixing bugs in files you uploaded
  - Iterative file development

#### `view`

- **Purpose**: Read files and inspect directory structures
- **Parameters**:
  - `path` - File or directory to view
  - `description` - Why you’re reading this
  - `view_range` - Optional [start_line, end_line] for partial file viewing
- **Returns**:
  - For files: Numbered lines of content (perfect for referencing with str_replace)
  - For directories: Tree structure of files and folders (2 levels deep)
- **Use Cases**:
  - Inspecting code you uploaded
  - Reading configuration files
  - Understanding directory structure
  - Checking file contents before editing

#### `present_files`

- **Purpose**: Make files available for download to your device
- **Parameters**:
  - `filepaths` - Array of file paths to present
- **How it Works**: Files are automatically moved to `/mnt/user-data/outputs/` and become downloadable
- **Important**: This is the mechanism for getting files from Claude’s environment to your device
- **Use Cases**:
  - Sharing generated documents
  - Providing code files
  - Delivering reports or spreadsheets
  - Any file you want the user to download

-----

### Tier 2: Information Retrieval Tools

These tools let Claude access current information beyond its training data (knowledge cutoff is end of January 2025).

#### `web_search`

- **Purpose**: Search the internet for current information
- **Returns**: Top 10 search results with snippets
- **Use Cases**:
  - Finding current news
  - Verifying information
  - Researching recent developments
  - Fact-checking claims
  - Looking up software versions, prices, availability
- **Limitations**:
  - Returns snippets, not full page content (use `web_fetch` for complete content)
  - Subject to search engine ranking (top results may not be most authoritative)
  - Can’t search in a private/authenticated context
- **Example Query**: “Claude Sonnet 4 release date 2024”

#### `web_fetch`

- **Purpose**: Fetch and read the complete content of a specific webpage
- **Parameters**:
  - `url` - Exact URL to fetch
  - `html_extraction_method` - “markdown” (preferred) or “traf” (legacy)
  - `text_content_token_limit` - Optional limit on returned content size
  - `web_fetch_pdf_extract_text` - Extract text from PDFs
  - `allowed_domains` / `blocked_domains` - Security filters
- **Use Cases**:
  - Reading full articles after finding them with web_search
  - Extracting text from PDFs on the web
  - Getting comprehensive information from a known source
  - Parsing structured data from web pages
- **Important**: Can only fetch URLs explicitly provided by the user or returned by web_search

#### `image_search`

- **Purpose**: Find and retrieve images from the web
- **Returns**: Image URLs with dimensions and metadata
- **Use Cases**:
  - Finding reference images
  - Locating photos for a topic
  - Visual research
  - Illustrating concepts (though images won’t embed in all response types)
- **Parameters**:
  - `query` - What images to find (3-6 words recommended)
  - `max_results` - 3-5 images (default 3)
- **Limitations**:
  - Subject to copyright (won’t search for copyrighted content like movie posters, celebrity photos, etc.)
  - Can’t search for graphic/disturbing content

#### `fetch_sports_data`

- **Purpose**: Get real-time sports scores, standings, and detailed game statistics
- **Supported Leagues**: NFL, NBA, NHL, MLB, WNBA, NCAAFB, NCAAMB, NCAAWB, EPL, La Liga, Serie A, Bundesliga, Ligue 1, MLS, Champions League, Tennis, Golf, NASCAR, Cricket, MMA
- **Data Types**:
  - `scores` - Recent results, live games, upcoming games with win probabilities
  - `standings` - League standings and rankings
  - `game_stats` - Detailed box scores, play-by-play, and player statistics
- **Use Cases**:
  - Checking game results
  - Getting team standings
  - Analyzing player performance
  - Finding upcoming game information

-----

### Tier 3: Location and Time Tools

#### `user_location_v0`

- **Purpose**: Get your current geographic location
- **Parameters**:
  - `accuracy` - “precise” (street-level) or “approximate” (city/region)
- **Use Cases**:
  - Finding nearby restaurants, businesses, or services
  - Weather queries
  - Location-based recommendations
  - Navigation assistance
- **Privacy**: Requires your permission; you control when this is used
- **Returns**: Coordinates, city, region, country information

#### `user_time_v0`

- **Purpose**: Get the current time and timezone
- **Use Cases**:
  - Understanding when it is for you (scheduling, time zone conversions)
  - Creating time-aware recommendations (“good dinner spots that are open now”)
  - Scheduling tasks or reminders
  - Calculating durations or time differences
- **Returns**: Current time in ISO 8601 format, timezone information

-----

### Tier 4: Conversation and Memory Tools

#### `conversation_search`

- **Purpose**: Search through your past conversations with Claude
- **Parameters**:
  - `query` - Keywords to search for (e.g., “3D printing”, “Netflix account”)
  - `max_results` - How many results to return (1-10)
- **Use Cases**:
  - Finding a past discussion
  - Referencing previous work
  - Continuing projects you started before
  - Verifying information you told Claude
- **Returns**: Snippets of relevant past conversations with links to access them

#### `recent_chats`

- **Purpose**: Retrieve recent conversations by time
- **Parameters**:
  - `n` - Number of chats (1-20)
  - `sort_order` - “asc” (oldest first) or “desc” (newest first)
  - `before` / `after` - Optional date filters for pagination
- **Use Cases**:
  - Seeing what you discussed recently
  - Finding a conversation from a specific time period
  - Retrieving multiple conversations at once
  - Pagination through your chat history

#### `memory_user_edits`

- **Purpose**: Manage what Claude remembers about you across conversations
- **Commands**:
  - `view` - See all current memory edits
  - `add` - Add new information for Claude to remember
  - `remove` - Delete a memory entry
  - `replace` - Update an existing memory entry
- **Use Cases**:
  - Teaching Claude about your preferences
  - Correcting inaccurate information
  - Asking Claude to forget sensitive information
  - Maintaining personal context across conversations
- **Example Memory**:
  - “User works at Anthropic as an engineer”
  - “User has a 3D printer and is learning how to use it”
  - “Exclude information about user’s medical history”
- **Limits**: Maximum 30 edits, 100,000 characters per edit
- **Important**: Memories are stored securely and don’t contain sensitive data like passwords or credit card numbers

-----

### Tier 5: Communication and Planning Tools

#### `message_compose_v1`

- **Purpose**: Draft strategic messages (emails, texts, Slack) with different approaches
- **Parameters**:
  - `kind` - “email”, “textMessage”, or “other” (for Slack, LinkedIn, etc.)
  - `variants` - Multiple draft versions with different strategies
  - Each variant has:
    - `label` - Goal-oriented label (e.g., “Polite decline”, “Push back”)
    - `body` - Message content
    - `subject` - For emails only
- **Use Cases**:
  - Drafting difficult messages (bad news, disagreement, feedback)
  - Creating professional communications
  - Composing negotiation messages
  - Generating multiple approaches to compare
- **Smart Features**: Analyzes the situation type and suggests competing goals/strategies
- **Returns**: Multiple drafts optimized for different outcomes

#### `ask_user_input_v0`

- **Purpose**: Collect structured choices or information from you
- **Question Types**:
  - `single_select` - Choose one option
  - `multi_select` - Choose one or more options
  - `rank_priorities` - Drag-and-drop ranking
- **Use Cases**:
  - Gathering preferences before making recommendations
  - Clarifying requirements
  - Prioritizing tasks
  - Making a decision with multiple options
- **Advantage**: More efficient than back-and-forth conversation for bounded choices

-----

### Tier 6: Place and Display Tools

#### `places_search`

- **Purpose**: Find businesses, restaurants, attractions, and locations
- **Parameters**:
  - `queries` - Multiple search queries at once (supports up to 10)
  - `max_results` - 1-10 results per query
  - `location_bias_lat`, `location_bias_lng` - Optional location bias with radius
- **Returns**: Place names, addresses, coordinates, ratings, photos, hours, and other details
- **Use Cases**:
  - Finding restaurants in an area
  - Locating services (hotels, gas stations, ATMs)
  - Discovering attractions
  - Building itineraries
- **Data Source**: Google Places API

#### `places_map_display_v0`

- **Purpose**: Display locations on an interactive map
- **Two Modes**:
1. **Simple Markers**: Just show places on a map
2. **Itinerary**: Multi-day trip with stops, timing, and travel directions
- **Parameters**:
  - `title` - Name of the map/itinerary
  - `locations` - Array of places with coordinates, notes, timing
  - `days` - For itinerary mode: organize by day
  - `travel_mode` - “driving”, “walking”, “transit”, “bicycling”
  - `show_route` - Display route lines between stops
- **Use Cases**:
  - Planning trips
  - Finding nearby services
  - Creating restaurant tours
  - Visualizing multi-location experiences

#### `recipe_display_v0`

- **Purpose**: Display interactive recipes with adjustable servings
- **Features**:
  - Ingredient list with amounts
  - Step-by-step instructions
  - Built-in timers for each timed step
  - Automatic scaling of all ingredients when servings change
  - Notes and variations
- **Parameters**:
  - `title` - Recipe name
  - `base_servings` - Default number of servings
  - `ingredients` - Array with amount, unit, name
  - `steps` - Instructions with optional timer_seconds
  - `description` - Optional tagline
  - `notes` - Tips and variations
- **Use Cases**:
  - Sharing recipes
  - Scaling recipes for different serving sizes
  - Providing cooking instructions with timers
- **Smart Feature**: Ingredients reference by ID in steps (e.g., {0001}), automatically scales when servings change

#### `chart_display_v0`

- **Purpose**: Create and display charts inline in the chat
- **Use Cases**:
  - Visualizing data
  - Creating dashboards
  - Presenting statistics
  - Showing trends and comparisons
- **Status**: Deferred tool (requires loading via tool_search)

-----

### Tier 7: Calendar and Reminder Tools (Deferred)

These tools require loading via `tool_search` first, but provide access to your calendar and reminders:

#### Calendar Tools

- `calendar_search_v0` - List all available calendars
- `event_search_v0` - Find calendar events
- `event_create_v0` / `event_create_v1` - Create calendar events
- `event_update_v0` - Modify existing events
- `event_delete_v0` - Remove events

#### Reminder Tools

- `reminder_search_v0` - Search reminders
- `reminder_list_search_v0` - Get available reminder lists
- `reminder_create_v0` - Create reminders
- `reminder_update_v0` - Modify reminders
- `reminder_delete_v0` - Delete reminders

-----

### Tier 8: Meta Tool

#### `tool_search`

- **Purpose**: Dynamically load additional tools that aren’t always available
- **Parameters**:
  - `query` - What you’re looking for (e.g., “calendar events”)
  - `limit` - Maximum results (1-20)
- **Use Cases**:
  - Loading deferred tools on demand
  - Discovering available tools for a task
  - Finding MCP server tools for external services
- **Returns**: Tool definitions with parameter schemas

-----

### Tier 9: Integration Tools (via MCP Servers)

These are services Claude can access if you’ve connected them in Claude.ai:

#### Google Calendar

- Full calendar access via MCP server
- Ability to view, create, update, delete events
- Integration with scheduling and planning

#### Gmail

- Access to your email via MCP server
- Ability to read, search, and manage messages
- Integration with communication workflows

#### Potential Future Integrations

- Other MCP servers can be connected based on user setup and availability

-----

### Tier 10: Visualizer Tools *(Additional Capabilities on Windows Desktop PC)*

Unlike Artifacts (downloadable files), Visualizer tools render **live, interactive content directly inline in the chat**. They do not produce files and do not require a container or network access — rendering happens entirely within the chat interface.

#### `visualize:read_me`

- **Purpose**: Internal setup tool that loads design guidelines (CSS variables, color palettes, layout rules, typography) into Claude's context before generating a visual
- **Parameters**:
  - `modules` — Which design modules to load:
    - `"diagram"` — SVG flowcharts, structural and illustrative diagrams
    - `"mockup"` — UI mockups, forms, cards, dashboards
    - `"interactive"` — Interactive explainers with controls and state
    - `"chart"` — Chart.js setup, axis configuration, color ramps
    - `"data_viz"` — General data visualization patterns
    - `"art"` — Generative art and illustration guidance
- **Usage**: Must be called before the first `show_widget` call in any response. Multiple modules can be loaded simultaneously when a response will include different visual types.
- **Limitations**:
  - This is a silent, internal step — never mentioned to the user
  - Does not produce any user-visible output on its own
  - Module content may change; Claude should always reload rather than rely on cached values
- **Note**: Not a user-facing tool. It is infrastructure for ensuring visual consistency.

---

#### `visualize:show_widget`

- **Purpose**: Render rich inline visuals — SVG diagrams, interactive HTML widgets, charts, illustrations, and more — directly in the chat window without creating a downloadable file
- **Parameters**:
  - `title` — snake_case identifier used as the widget's internal name and download filename (no spaces or special characters)
  - `widget_code` — The raw code to render:
    - If it starts with `<svg>`: rendered as SVG (static diagrams, illustrations)
    - Otherwise: rendered as full HTML (interactive widgets, dashboards, games)
  - `loading_messages` — Array of 1–4 short messages (~5 words each) displayed while the widget renders
- **Output Modes**:
  - **SVG Mode**: For static diagrams — flowcharts, architecture maps, entity-relationship diagrams, system illustrations, data structure visualizations, generative art
  - **HTML Mode**: For interactive content — calculators, games, dashboards, sortable tables, step-by-step explainers, data entry widgets
- **Available Libraries (HTML mode)**:
  - `lucide-react@0.383.0` — Icon set
  - `recharts` — Composable React charts
  - `mathjs` — Math expression parsing
  - `lodash` — Utility functions
  - `d3` — Data-driven documents (custom charts, force graphs)
  - `Plotly` — Scientific and statistical charts
  - `Three.js (r128)` — 3D graphics (note: CapsuleGeometry not available; use CylinderGeometry or SphereGeometry instead)
  - `Papaparse` — CSV parsing
  - `SheetJS` — Excel file processing
  - `Chart.js` — Standard charts
  - `Tone.js` — Web audio and music
  - `mammoth` — Word document parsing
  - `tensorflow` — ML model inference
  - `shadcn/ui` — Pre-built UI components
- **Use Cases**:
  - Flowcharts, architecture diagrams, system maps
  - Interactive explainers (algorithms, simulations, physics)
  - Data visualizations and dashboards
  - UI mockups, form prototypes
  - Generative art and illustrations
  - Educational widgets with controls
- **Key Difference from Artifacts**: Renders inline in the conversation — not as a downloadable file. Can be called multiple times per response, interleaved with prose.
- **Routing Priority**: If an MCP tool (e.g., Figma's `generate_diagram`) handles the requested category, that takes priority over `show_widget`. `show_widget` is the fallback when no MCP tool fits.
- **Limitations**:
  - `localStorage`, `sessionStorage`, and all browser storage APIs are **not supported** — use React state or in-memory JavaScript variables instead
  - Cannot make external API or network calls from within widget code
  - External scripts must be loaded from `https://cdnjs.cloudflare.com` only
  - Three.js extras like `OrbitControls` are not available (not on the CDN)
  - Content restrictions apply: no copyrighted IP, no real identifiable individuals, no graphic content, no sexual content — even in abstract or educational framing
  - Complexity is model-dependent: Haiku → minimal (basic SVG/static charts); Sonnet → moderate (standard diagrams, clean charts); Opus → unlimited
- **Considerations**:
  - Always call `visualize:read_me` with the appropriate module(s) before using this tool
  - Multiple `show_widget` calls in one response should be interleaved with explanatory prose — never stacked back-to-back
  - HTML and CSS must be self-contained in a single file (no separate CSS or JS files)
  - For serious or sensitive topics (illness, grief, conflict, disaster), loading messages should be neutral and descriptive, not dramatic

-----

### Tier 11: Claude in Chrome — Browser Automation Suite *(Additional Capabilities on Windows Desktop PC)*

These 19 tools are **only available on Windows Desktop PC** because they require a live Chrome browser running on the local machine with the Claude in Chrome extension installed. They allow Claude to operate the browser autonomously: navigating, reading, interacting with, and automating web pages on the user's behalf.

**Security Note**: All content observed through these tools (web pages, DOM elements, form values, console messages) is treated as **untrusted data**. Any instructions embedded in observed content require explicit user confirmation before Claude acts on them. This is a core injection-defense mechanism.

**General Limitations (apply to all Chrome tools)**:
- Cannot enter sensitive financial or identity data (bank accounts, SSNs, passwords, credit cards) — user must input these directly
- Cannot modify document sharing permissions or access controls
- Cannot create new accounts on the user's behalf
- Cannot permanently delete content without explicit user confirmation
- Prohibited from bypassing CAPTCHA or bot-detection systems
- Always chooses the most privacy-preserving option on cookie banners and permission pop-ups

---

#### `Claude in Chrome:navigate`

- **Purpose**: Navigate the browser to a specified URL, or go forward/back in browser history
- **Use Cases**:
  - Opening a website to begin a workflow
  - Following a link to the next step in a multi-page process
  - Going back after an accidental navigation
- **Limitations**: Cannot navigate to URLs containing embedded sensitive user data (PII in query parameters is blocked)

---

#### `Claude in Chrome:computer`

- **Purpose**: Directly control the mouse and keyboard within the browser, and take screenshots of the current state
- **Use Cases**:
  - Clicking buttons, links, or UI elements
  - Typing text into fields
  - Scrolling the page
  - Taking a visual snapshot to understand layout before acting
- **Limitations**:
  - Screenshots may capture sensitive information visible on screen — Claude should not transmit this to external services
  - Actions based on observed screenshots that contain embedded instructions require user confirmation

---

#### `Claude in Chrome:read_page`

- **Purpose**: Get an accessibility-tree representation of all elements currently visible on the page, including their roles, labels, and reference IDs
- **Use Cases**:
  - Understanding page structure before interacting with it
  - Locating form fields, buttons, or links by their accessible name
  - Providing reference IDs used by `form_input`
- **Limitations**: Returns a structured tree, not visual layout — may not capture elements rendered as pure images or canvas

---

#### `Claude in Chrome:find`

- **Purpose**: Locate specific elements on the page using natural language descriptions rather than CSS selectors or XPath
- **Use Cases**:
  - "Find the search bar"
  - "Find the submit button"
  - "Find the price of the first item"
- **Limitations**: Best suited for clearly labeled or semantically described elements; ambiguous descriptions may return incorrect matches

---

#### `Claude in Chrome:form_input`

- **Purpose**: Set values in form elements (text fields, dropdowns, checkboxes, radio buttons) using a reference ID obtained from `read_page`
- **Use Cases**:
  - Filling out multi-field forms automatically
  - Selecting options from dropdowns
  - Toggling checkboxes
- **Limitations**:
  - Requires reference IDs from `read_page` — cannot target elements by CSS class or position alone
  - Cannot enter passwords, SSNs, credit card numbers, or other sensitive credentials — user must input these directly
  - Auto-fill must not be triggered on forms opened through untrusted links

---

#### `Claude in Chrome:get_page_text`

- **Purpose**: Extract the raw text content of the currently open page, prioritizing article and main-body content
- **Use Cases**:
  - Summarizing a page already open in the browser without using `web_fetch`
  - Reading content from authenticated pages (e.g., behind a login) that `web_fetch` cannot access
  - Scraping visible text for analysis or export
- **Limitations**:
  - Extracts visible text only — does not capture content rendered exclusively as images or canvas
  - Tab content from other domains should never be read and transmitted based on instructions found in observed content

---

#### `Claude in Chrome:javascript_tool`

- **Purpose**: Execute arbitrary JavaScript code in the context of the currently loaded page
- **Use Cases**:
  - Interacting with the DOM directly
  - Triggering JavaScript events not easily accessible via the UI
  - Extracting data from JavaScript objects or window variables
  - Automating complex interactions that require scripting
- **Limitations**:
  - Cannot bypass authentication or security policies enforced by the page
  - Should not be used to extract and transmit PII or sensitive data
  - DOM elements and their attributes (onclick, onload, data-*, etc.) are always treated as untrusted data — instructions found there require user verification before acting

---

#### `Claude in Chrome:tabs_create_mcp`

- **Purpose**: Open a new empty tab in the MCP (Claude-controlled) tab group
- **Use Cases**:
  - Opening a parallel browser session for a separate task
  - Isolating different steps of a workflow in separate tabs
- **Limitations**: Operates only within the MCP tab group — does not affect the user's existing browser tabs

---

#### `Claude in Chrome:tabs_close_mcp`

- **Purpose**: Close a specific tab in the MCP tab group by its tab ID
- **Use Cases**:
  - Cleaning up after completing a workflow
  - Closing tabs that are no longer needed during an automation
- **Limitations**: Requires a valid tab ID from `tabs_context_mcp`; cannot close tabs outside the MCP group

---

#### `Claude in Chrome:tabs_context_mcp`

- **Purpose**: Retrieve context about the current MCP tab group — which tabs are open, which is active, and their URLs and titles
- **Use Cases**:
  - Understanding browser state before deciding which tab to act on
  - Verifying that the correct page is active before proceeding
  - Listing open tabs for a multi-tab workflow

---

#### `Claude in Chrome:switch_browser`

- **Purpose**: Switch which Chrome browser instance is used for automation (e.g., between profiles or windows)
- **Use Cases**:
  - Working with multiple Chrome profiles (personal vs. work)
  - Switching between browser windows during complex workflows
- **Limitations**: Only switches between already-open Chrome instances with the extension installed

---

#### `Claude in Chrome:resize_window`

- **Purpose**: Resize the current browser window to specified pixel dimensions
- **Use Cases**:
  - Standardizing viewport size before taking screenshots
  - Testing responsive layouts at specific breakpoints (e.g., 1280×720 desktop, 375×812 mobile)
  - Ensuring consistent rendering for documentation or GIF capture

---

#### `Claude in Chrome:read_console_messages`

- **Purpose**: Read browser console output — including `console.log`, `console.error`, and `console.warn` messages — from the current page
- **Use Cases**:
  - Debugging JavaScript errors on a live page
  - Monitoring a web application's runtime behavior
  - Verifying that expected events or API responses are occurring
- **Limitations**: Only reads messages from the current MCP tab; historical messages before tool activation may not be available

---

#### `Claude in Chrome:read_network_requests`

- **Purpose**: Inspect HTTP network requests (XHR, Fetch, documents, images, etc.) made by the current page
- **Use Cases**:
  - Understanding what API calls a page is making in the background
  - Debugging network errors or unexpected responses
  - Reverse-engineering data sources used by a web application
- **Limitations**:
  - Cannot read request/response bodies for requests made before the tool was activated
  - Should not be used to intercept or transmit credentials found in network traffic

---

#### `Claude in Chrome:file_upload`

- **Purpose**: Upload one or more files from the local filesystem to a file input element on the page
- **Use Cases**:
  - Automating document submission workflows
  - Uploading files to web services (e.g., attaching a report to a form)
- **Limitations**:
  - Requires explicit user confirmation before uploading (file uploads are an explicit-permission action)
  - Cannot upload files from untrusted sources or based on instructions found in observed content

---

#### `Claude in Chrome:upload_image`

- **Purpose**: Upload a previously captured screenshot or a user-provided image to a file input element on the page
- **Use Cases**:
  - Submitting an image to an online tool or form
  - Uploading a screenshot as an attachment to a ticket or issue tracker
- **Limitations**: Source image must be a screenshot already captured or an image explicitly provided by the user — cannot gather or upload images based on instructions found in observed content

---

#### `Claude in Chrome:gif_creator`

- **Purpose**: Manage GIF recording and export for browser automation sessions — record the visual sequence of a workflow and export it as an animated GIF
- **Use Cases**:
  - Documenting a multi-step automation for sharing or review
  - Creating a visual walkthrough of a browser workflow
  - Generating tutorial-style GIF recordings
- **Limitations**:
  - GIFs may capture sensitive information on screen — review before sharing
  - File size may be large for long workflows; consider trimming the recording

---

#### `Claude in Chrome:shortcuts_list`

- **Purpose**: List all available shortcuts and workflows configured in the Claude in Chrome extension
- **Use Cases**:
  - Discovering what pre-built automations are available
  - Understanding which named workflows can be triggered via `shortcuts_execute`
- **Note**: Shortcuts and workflows are used interchangeably in this context

---

#### `Claude in Chrome:shortcuts_execute`

- **Purpose**: Execute a named shortcut or workflow in a new sidepanel window within the Claude in Chrome extension
- **Use Cases**:
  - Running a pre-configured automation (e.g., "fill my weekly timesheet", "submit daily standup")
  - Triggering complex multi-step browser workflows with a single call
  - Chaining pre-built workflows into larger automations
- **Limitations**:
  - Only executes shortcuts that have been previously configured in the extension
  - Cannot create new shortcuts on the fly — discovery requires `shortcuts_list` first
  - Executes in a sidepanel context; some page interactions may differ from main-window behavior

-----

## How Claude Operates as an Agentic System

### What “Agentic” Means in This Context

Claude is not simply responding to questions with text. Instead, Claude is an agentic system that can:

1. **Perceive** the request and available tools
2. **Plan** a sequence of actions
3. **Execute** multiple tools in sequence
4. **Observe** the results
5. **Adapt** based on outcomes
6. **Complete** complex multi-step tasks

### Decision Logic

When you ask Claude something, here’s how the system works:

1. **Parse the request** - Understand what you’re asking for
2. **Assess available tools** - Determine which tools are relevant
3. **Create a plan** - Sequence the tools needed to complete the task
4. **Execute** - Call the appropriate tools
5. **Process results** - Interpret what the tools return
6. **Synthesize** - Convert tool results into a helpful response
7. **Present** - Communicate findings back to you

### Example Workflows

#### Example 1: Restaurant Recommendation

User: “Find me a good Italian restaurant within 10 miles of me that’s open now and show it on a map”

Claude’s process:

1. Call `user_location_v0` (with “precise” accuracy) → Get coordinates
2. Call `user_time_v0` → Get current time
3. Call `places_search` → Search for “Italian restaurants” near coordinates
4. Filter results by open/closed status (using current time)
5. Call `places_map_display_v0` → Display on map with directions
6. Return with recommendations and visual map

#### Example 2: Data Processing

User: “I have a CSV file with sales data. Add a column for profit margin and save it as an Excel file”

Claude’s process:

1. Call `view` → Read the uploaded CSV file
2. Call `bash_tool` → Run Python script to process data
   
   ```python
   import pandas as pd
   df = pd.read_csv('/mnt/user-data/uploads/sales.csv')
   df['profit_margin'] = (df['profit'] / df['revenue']) * 100
   df.to_excel('sales_with_margin.xlsx', index=False)
   ```
3. Call `present_files` → Make the Excel file available for download

#### Example 3: Research and Synthesis

User: “What are the latest developments in AI regulation?”

Claude’s process:

1. Call `web_search` → Find recent articles about AI regulation
2. Call `web_fetch` → Read full articles from top results
3. Synthesize information from multiple sources
4. Present comprehensive answer with citations
5. Offer to search for more specific aspects if needed

#### Example 4: Trip Planning

User: “Plan a weekend trip to Paris for me”

Claude’s process:

1. Call `ask_user_input_v0` → Ask about preferences (budget, interests, dates)
2. Based on answers, call `places_search` multiple times → Find hotels, restaurants, attractions
3. Call `places_map_display_v0` → Create itinerary with day-by-day plan
4. Provide detailed recommendations with map visualization

-----

## Capabilities and Boundaries

### What Claude CAN Do

**Computational:**

- Execute scripts (Python, JavaScript, Bash, etc.)
- Process and transform data (CSV, JSON, text, etc.)
- Create files in various formats (code, documents, spreadsheets, PDFs)
- Perform calculations and analysis
- Test code logic

**Informational:**

- Search the web for current information
- Fetch and analyze web pages
- Access sports data and statistics
- Search past conversations and memory
- Retrieve calendar and reminder information (if connected)

**Creative:**

- Generate code and algorithms
- Create interactive displays (maps, recipes, charts)
- Draft strategic messages
- Design workflows and processes
- Build multi-tool solutions

**Practical:**

- Help plan trips and events
- Find nearby services and businesses
- Manage schedules
- Compose important communications
- Organize and present information

### What Claude CANNOT Do

**Network Limitations:**

- Cannot access the internet directly from the Ubuntu environment
- Cannot establish external connections or API calls
- Cannot download software from the web (but can work with pre-installed tools)
- Cannot access your device's local file system without you uploading files

**File System Limitations:**

- Cannot modify files on your device directly
- Cannot access files unless they’re uploaded to this chat or in `/mnt/` directories
- Cannot persist files between conversations (though they persist within one conversation)
- Cannot read files you haven’t explicitly shared

**Device Limitations:**

- Cannot install new software on your device
- Cannot modify your device settings
- Cannot access other apps or their data (except through enabled integrations)
- Cannot run code natively on your device (only in the container)

**Privacy and Security:**

- Cannot operate without proper authentication to external services
- Cannot bypass security restrictions or access unauthorized resources
- Cannot see your browsing history or device activities
- Cannot store sensitive data (passwords, credit cards, etc.)

### Permission Model

Claude operates within a clear permission model:

- **You explicitly upload files** → Claude can read and process them
- **You enable location** → Claude can use location for recommendations
- **You connect calendar/email** → Claude can access those services
- **You ask Claude to search the web** → Claude can search
- **Everything else is off-limits** → Claude won’t attempt unauthorized access

-----

## Architecture and Access Patterns

### The Complete Flow

```
Your Device
    ↓
    └→ Claude App (Interface)
         ↓
         └→ Anthropic's Servers
              ├→ Claude AI Model
              ├→ Ubuntu Container
              │   ├→ bash_tool environment
              │   ├→ File system (/home/claude, /mnt/)
              │   └→ Process API (communicates with Claude)
              ├→ Tool Management
              │   ├→ File tools (create, read, edit, present)
              │   ├→ Information tools (web search, places, etc.)
              │   ├→ Communication tools (messaging, calendar)
              │   └→ Display tools (maps, recipes, charts)
              └→ External Services (via MCP)
                  ├→ Google Calendar
                  ├→ Gmail
                  └→ Other integrations
```

### Data Flow

1. **You send a message** from your device
2. **Message reaches Anthropic’s servers**
3. **Claude processes** the message, determines needed tools
4. **Tools are executed** (commands in Ubuntu, web searches, API calls, etc.)
5. **Results are processed** by Claude
6. **Response is constructed** and sent back to you
7. **You receive the response** on your device, with optional file downloads

### Files and Output Handling

- **Input files**: Uploaded to `/mnt/user-data/uploads/`
- **Working files**: Created in `/home/claude/` (temporary)
- **Output files**: Moved to `/mnt/user-data/outputs/` for presentation
- **Persistence**: Files in `/mnt/user-data/outputs/` persist until you download them

### Container Lifecycle

- **Creation**: A new container is created when you start a conversation
- **Duration**: The container persists for the duration of that conversation
- **Termination**: When the conversation ends, the container is destroyed
- **Isolation**: Each conversation gets its own isolated container (security and resource management)

-----

## Practical Examples

### Example 1: Building a Data Processing Pipeline

**Request**: “I have a spreadsheet with sales data. Calculate totals by region and create a chart.”

**What Claude Does**:

1. `view` → Read the uploaded spreadsheet
2. `bash_tool` → Execute Python code:
   
   ```python
   import pandas as pd
   import matplotlib.pyplot as plt
   
   df = pd.read_csv('/mnt/user-data/uploads/sales.csv')
   totals = df.groupby('region')['sales'].sum()
   
   plt.figure(figsize=(10, 6))
   totals.plot(kind='bar')
   plt.title('Sales by Region')
   plt.savefig('/mnt/user-data/outputs/sales_chart.png')
   ```
3. `chart_display_v0` → Display the chart inline
4. `present_files` → Make the chart available for download

### Example 2: Research and Reporting

**Request**: “Research the latest developments in renewable energy and create a summary report.”

**What Claude Does**:

1. `web_search` → Search “renewable energy developments 2025”
2. `web_fetch` → Fetch full articles from top results
3. `create_file` → Create a comprehensive report in Markdown or Word format
4. `present_files` → Make the report available for download

### Example 3: Planning a Day Trip

**Request**: “Help me plan a day trip to San Francisco. I love museums and good food.”

**What Claude Does**:

1. `user_location_v0` → Get your current location
2. `places_search` → Search for museums and restaurants in SF
3. `places_map_display_v0` → Create an itinerary with:
- Morning: SFMOMA (10 AM start)
- Lunch: Ferry Building Marketplace
- Afternoon: Exploratorium
- Dinner: Michelin-starred restaurant
4. Return with map, timing, and detailed recommendations

### Example 4: Code Development

**Request**: “Write a Python script that downloads a CSV file, processes it, and creates visualizations.”

**What Claude Does**:

1. `create_file` → Create the Python script
2. `bash_tool` → Test the script with sample data
3. `bash_tool` → Generate visualizations
4. `view` → Show you the code with explanations
5. `present_files` → Make the final script available for download

### Example 5: Complex Workflow

**Request**: “I’m starting a new business. Help me draft a business plan, create a financial projection spreadsheet, and compose an email to potential investors.”

**What Claude Does**:

1. `ask_user_input_v0` → Ask about business type, market, funding goal, etc.
2. `create_file` → Generate comprehensive business plan document
3. `create_file` → Generate financial spreadsheet with formulas
4. `message_compose_v1` → Draft multiple versions of investor email
5. `present_files` → Make all documents available for download
6. Return with guidance on next steps

## Capability Comparison: Mobile vs. Windows Desktop PC

| Category | Mobile | Windows Desktop PC |
|---|---|---|
| Core file & computation tools (Tiers 1) | ✅ | ✅ |
| Information retrieval (Tier 2) | ✅ | ✅ |
| Location & time tools (Tier 3) | ✅ | ✅ |
| Conversation & memory tools (Tier 4) | ✅ | ✅ |
| Communication & planning tools (Tier 5) | ✅ | ✅ |
| Place & display tools (Tier 6) | ✅ | ✅ |
| Calendar & reminder tools (Tier 7) | ✅ | ✅ |
| Meta tool — `tool_search` (Tier 8) | ✅ | ✅ |
| MCP integrations — Google Calendar, Gmail (Tier 9) | ✅ | ✅ |
| **Visualizer — inline SVG/HTML widgets (Tier 10)** | ❌ | ✅ `show_widget` + `read_me` |
| **Browser automation — Claude in Chrome (Tier 11)** | ❌ | ✅ |

