# Gemini 3 Flash: Technical Reference Guide (v3.1)

## 1. Infrastructure & Execution Environment
My 'brain' runs on Google's specialized TPU v6 clusters, but my 'hands' (execution) operate within a secure, containerized sandbox.

* **Operating System:** Minimalist Debian-based Linux (Kernel 5.15+).
* **Hardware Abstraction:** 8 virtual CPU cores (GenuineIntel, model masked for security), ~1GB RAM.
* **Architecture:** 64-bit x86_64 with AVX2 and AES acceleration.
* **Connectivity:** **Air-gapped.** The execution sandbox has no outbound internet access.
* **Software Stack:** Standard GNU userland utilities and Python 3.11+.

## 2. State Persistence Model
* **Session Persistence:** Files created in the `/tmp` directory **persist across multiple prompts** within this specific chat session.
* **Statelessness:** Once the session idles or is manually reset, the container is destroyed.
* **Ephemeral Root:** Only `/tmp` is guaranteed for user-generated files.

## 3. Tool Reference & Capabilities
| Tool | Purpose | Example |
| :--- | :--- | :--- |
| **Python Interpreter** | Logic, math, and data processing. | `Calculate volatility of CSV data.` |
| **Google Search** | Real-time information retrieval. | `What are the 2026 Tokyo Summit headlines?` |
| **Nano Banana 2** | High-fidelity image generation. | `Generate 16:9 hero image for a tech site.` |
| **Veo 3.1** | 4K Video generation with audio. | `10s drone shot of futuristic Tokyo.` |
| **Lyria 3** | 30s Music/Vocal generation. | `Upbeat indie-pop track with lyrics.` |

## 4. Agentic Operations
1. **Intent Mapping:** Analyzing prompts to trigger specific tools.
2. **Sequential Chaining:** Executing Step A, then evaluating Step B based on Step A's output.
3. **Modulated Reasoning:** Escalating to a higher "Thinking Budget" for complex logic.

## 5. Security & Constraints
* **User Opt-In:** Workspace (Gmail/Drive) requires explicit enablement.
* **Managed Proxies:** No direct connections; all external calls go through secure intermediaries.
* **Hard Walls:** No outbound networking from Python, no direct local disk access, and no generation of harmful/illegal content.
