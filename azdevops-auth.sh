#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# azdevops-auth.sh
#
# Authenticates the Azure CLI + azure-devops extension using a PAT and
# persists the org / project defaults so every subsequent `az devops` command
# just works — including when called from Claude Code.
#
# Usage:
#   ./azdevops-auth.sh                          # prompts for everything
#   ./azdevops-auth.sh <org-url> <project>      # prompts for PAT only
#
# Or set env vars to make it fully non-interactive:
#   export AZURE_DEVOPS_EXT_PAT="<your-pat>"
#   export AZDEVOPS_ORG="https://dev.azure.com/myorg"
#   export AZDEVOPS_PROJECT="MyProject"
#   ./azdevops-auth.sh
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

# ── Colour helpers ─────────────────────────────────────────────────────────
GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'
info()    { echo -e "${GREEN}[✔]${NC}  $*"; }
warn()    { echo -e "${YELLOW}[!]${NC}  $*"; }
error()   { echo -e "${RED}[✘]${NC}  $*" >&2; exit 1; }
ask()     { echo -e "${YELLOW}[?]${NC}  $*"; }

# ── 1. Check az CLI is installed ────────────────────────────────────────────
if ! command -v az &>/dev/null; then
  error "Azure CLI not found. Install it from https://aka.ms/installazureclimacos (macOS) or https://aka.ms/installazurecli"
fi
info "Azure CLI found: $(az version --query '\"azure-cli\"' -o tsv 2>/dev/null)"

# ── 2. Ensure azure-devops extension is present ─────────────────────────────
if ! az extension show --name azure-devops &>/dev/null; then
  warn "azure-devops extension not installed — installing now…"
  az extension add --name azure-devops --yes
fi
info "azure-devops extension ready"

# ── 3. Collect org URL ──────────────────────────────────────────────────────
ORG="${AZDEVOPS_ORG:-${1:-}}"
if [[ -z "$ORG" ]]; then
  ask "Enter your Azure DevOps organisation URL"
  ask "  Example: https://dev.azure.com/myorg  or  https://myorg.visualstudio.com"
  read -rp "  Org URL: " ORG
fi
# Normalise: strip trailing slash
ORG="${ORG%/}"
[[ -z "$ORG" ]] && error "Organisation URL cannot be empty."
info "Organisation: $ORG"

# ── 4. Collect project name ─────────────────────────────────────────────────
PROJECT="${AZDEVOPS_PROJECT:-${2:-}}"
if [[ -z "$PROJECT" ]]; then
  ask "Enter your Azure DevOps project name (leave blank to skip setting a default):"
  read -rp "  Project: " PROJECT
fi
[[ -n "$PROJECT" ]] && info "Project: $PROJECT"

# ── 5. Collect PAT ──────────────────────────────────────────────────────────
if [[ -z "${AZURE_DEVOPS_EXT_PAT:-}" ]]; then
  ask "Enter your Personal Access Token (input hidden):"
  read -rsp "  PAT: " AZURE_DEVOPS_EXT_PAT
  echo
fi
[[ -z "$AZURE_DEVOPS_EXT_PAT" ]] && error "PAT cannot be empty."
export AZURE_DEVOPS_EXT_PAT
info "PAT accepted (not echoed)"

# ── 6. Persist defaults in az CLI config ────────────────────────────────────
DEFAULTS="organization=$ORG"
[[ -n "$PROJECT" ]] && DEFAULTS="$DEFAULTS project=$PROJECT"
az devops configure --defaults $DEFAULTS
info "az devops defaults saved: $DEFAULTS"

# ── 7. Write a shell env file for Claude Code / shell sessions ───────────────
ENV_FILE="${HOME}/.azdevops_env"
cat > "$ENV_FILE" <<EOF
# Azure DevOps environment — sourced automatically by azdevops-auth.sh
# Regenerate by running azdevops-auth.sh again.
export AZURE_DEVOPS_EXT_PAT="$AZURE_DEVOPS_EXT_PAT"
export AZDEVOPS_ORG="$ORG"
${PROJECT:+export AZDEVOPS_PROJECT="$PROJECT"}
EOF
chmod 600 "$ENV_FILE"   # PAT is sensitive — owner-only
info "Environment written to $ENV_FILE (chmod 600)"

# ── 8. Optionally add auto-source to shell rc ───────────────────────────────
SHELL_RC="${HOME}/.zshrc"
[[ "${SHELL:-}" == *bash* ]] && SHELL_RC="${HOME}/.bashrc"

SOURCE_LINE="# Azure DevOps auth"$'\n'"[[ -f \"$ENV_FILE\" ]] && source \"$ENV_FILE\""
if ! grep -qF "$ENV_FILE" "$SHELL_RC" 2>/dev/null; then
  ask "Add auto-source of $ENV_FILE to $SHELL_RC? (y/N):"
  read -rp "  " REPLY
  if [[ "${REPLY:-n}" =~ ^[Yy]$ ]]; then
    echo -e "\n$SOURCE_LINE" >> "$SHELL_RC"
    info "Added to $SHELL_RC — will take effect in new shells"
  else
    warn "Skipped. To activate manually run:  source $ENV_FILE"
  fi
fi

# ── 9. Smoke test ────────────────────────────────────────────────────────────
echo ""
info "Running smoke test: az devops project list…"
if az devops project list --org "$ORG" --output table 2>/dev/null | head -20; then
  echo ""
  info "Authentication successful! 🎉"
  echo ""
  echo "  Subsequent az devops commands will use:"
  echo "    Organisation : $ORG"
  [[ -n "$PROJECT" ]] && echo "    Project      : $PROJECT"
  echo ""
  echo "  Reload your shell (or run: source $ENV_FILE) to pick up the env vars."
else
  echo ""
  error "Smoke test failed — check your PAT scopes (needs at least 'Read' on Code / Work Items) and org URL."
fi
