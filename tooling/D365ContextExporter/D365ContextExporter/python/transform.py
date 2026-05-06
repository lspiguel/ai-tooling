"""D365 Context Exporter — Jinja2 template orchestrator.

Reads intermediate.json, renders the specified Jinja2 template, and writes output.md.

Usage:
    python transform.py --input <path> --template <path> --out <dir> --spec <name>
"""

import argparse
import json
import os
import sys

try:
    from jinja2 import Environment, FileSystemLoader, StrictUndefined
    import filters as _filters_module
except ImportError as e:
    print(f"[transform] Import error: {e}", file=sys.stderr)
    print(
        "[transform] Ensure the virtual environment is active and requirements.txt is installed.",
        file=sys.stderr,
    )
    sys.exit(1)


def _count_tokens(text: str) -> int:
    try:
        import tiktoken
        enc = tiktoken.get_encoding("o200k_base")
        return len(enc.encode(text))
    except Exception:
        # Fallback: GPT-4o averages ~4 chars/token for English prose.
        return max(1, len(text) // 4)


def main() -> None:
    parser = argparse.ArgumentParser(description="Render a Jinja2 template from intermediate.json.")
    parser.add_argument("--input", required=True, help="Path to intermediate.json")
    parser.add_argument("--template", required=True, help="Path to the .j2 template file")
    parser.add_argument("--out", required=True, help="Output directory for output.md")
    parser.add_argument("--spec", required=True, help="Spec name injected as _spec")
    args = parser.parse_args()

    # Load intermediate JSON.
    with open(args.input, encoding="utf-8") as f:
        context = json.load(f)

    context["_spec"] = args.spec

    # Build Jinja2 environment.
    template_dir = os.path.dirname(os.path.abspath(args.template))
    template_name = os.path.basename(args.template)

    env = Environment(
        loader=FileSystemLoader(template_dir),
        undefined=StrictUndefined,
        keep_trailing_newline=True,
    )
    env.filters.update(_filters_module.get_filters())

    # Render.
    template = env.get_template(template_name)
    rendered = template.render(**context)

    # Write output.md.
    output_path = os.path.join(args.out, "output.md")
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(rendered)

    print(f"[transform] output.md written ({len(rendered.encode('utf-8'))} bytes)")

    # Count tokens and write sidecar.
    token_count = _count_tokens(rendered)
    token_path = os.path.join(args.out, "token_count.txt")
    with open(token_path, "w", encoding="utf-8") as f:
        f.write(str(token_count))
    print(f"[transform] token count (gpt-4o): {token_count:,}")


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"[transform] ERROR: {exc}", file=sys.stderr)
        import traceback
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)
