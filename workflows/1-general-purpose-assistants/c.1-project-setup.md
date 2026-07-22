# [C.1] Project Setup — the engagement reference: Context Exporter grounding for a general-purpose assistant

---

## What you need in place

| Requirement | Notes |
|---|---|
| XrmToolBox 1.2025.x or later | Installed as part of the machine baseline — `choco install xrmtoolbox -y` (see [B.1](./b.1-initial-setup.md) / [B.3](../3-coding-assistants/b.3-initial-setup.md)). |
| .NET Framework 4.8 | Required by the plugin; present on current Windows. |
| A connection to the **client's** Dataverse environment | System Customizer or System Administrator security role, or at minimum read access to entity metadata, solutions, security roles, and processes. |
| An enterprise/professional-tier AI assistant under the engagement's data agreement | The destination for the packs. Confirm the DPA / zero-retention terms *before* uploading anything. |

Do this once per client environment. If the engagement spans multiple environments (Dev / UAT / Prod), export from the one that is the source of truth for configuration — usually **Dev** — and note the environment URL in the Project so the assistant knows which tenant the grounding describes.

---

## 1. Prepare the local, personal Context repository

```
<client>-Context/
├── .gitignore
├── context-exporter/        D365 Context Exporter folder
│   ├── config/              Configuration files
│   ├── output/              Context packs to be supplied to the General AI Assistants
│   └── runs/                Ignored by .gitignore
├── XXX-story-1
│   ├── XXX-story-1.md       Replicated user stories content in markdown
│   ├── ...
│   └── ...
├── YYY-story-2
├── ZZZ-story-3
├── offline-access/
│   ├── deployment/
│   ├── guides/
│   ├── sow/
│   ├── ...
│   └── runbooks/
└── ...
```

---

## 2. Install the Context Exporter plugin

1. Open **XrmToolBox**.
2. Go to **Tool Library** (Configuration → Tool Library).
3. Search for **D365 CE Context Exporter** (or `D365ContextExporter`).
4. Click **Install**, then restart XrmToolBox when prompted.
5. In XrmToolBox, create or select a **connection** to the client's Dataverse environment.
6. Prefer a **Microsoft 365 / OAuth** connection (interactive sign-in or a service-principal app registration) over stored username/password.
7. Open the **D365 CE Context Exporter** tool against that connection.
8. Deploy the reference configuration to the per-client context folder (<client>-Context).
9. Run the specs.

---

## 3. Assemble the grounding

Create **one grounded Project per client** and seed it:

1. **Create the Project / Custom GPT / Copilot agent** in the enterprise-tier assistant, named for the client.
2. **Upload the `.context.md` packs** from `output\` as project knowledge.
3. **Add the engagement documents** the packs don't cover: the **SoW**, the **solution/architecture** doc, naming and publisher-prefix conventions, and any integration diagrams.
4. **Pin the client's data-handling rules** into the Project's custom instructions — a short paragraph stating the boundary (which tier is approved, what must never be pasted in, that outputs are drafts for human review). This makes the assistant remind *you* when a request would over-share.
5. **Record the source environment URL and the export date** in the instructions so everyone knows how fresh the grounding is and which tenant it reflects.

From here the assistant inherits the client's model on every [1.1] Specification / Intent, [2.1] Planning / Design, and [5.1] Validation conversation without re-explaining it.

---

## 4. Refresh cadence

The packs are a **point-in-time snapshot**. Re-run the exporter and re-upload when the model has moved materially — after a solution import to Dev, a wave of new tables/fields, or a security-role change. Treat regenerating the packs as the [6.1] Documentation step: the same artifact that grounds the assistant *is* the living data-model doc. Keep the export date in the Project instructions current each time.
