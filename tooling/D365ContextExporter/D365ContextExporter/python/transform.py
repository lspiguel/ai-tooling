"""D365 Context Exporter — Jinja2 template orchestrator.

Reads intermediate.json, renders the specified Jinja2 template, and writes output.md.

Usage:
    python transform.py --input <path> --template <path> --out <dir> --project <name>
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


def main() -> None:
    parser = argparse.ArgumentParser(description="Render a Jinja2 template from intermediate.json.")
    parser.add_argument("--input", required=True, help="Path to intermediate.json")
    parser.add_argument("--template", required=True, help="Path to the .j2 template file")
    parser.add_argument("--out", required=True, help="Output directory for output.md")
    parser.add_argument("--project", required=True, help="Project name injected as _project")
    args = parser.parse_args()

    # Load intermediate JSON.
    with open(args.input, encoding="utf-8") as f:
        context = json.load(f)

    context["_project"] = args.project

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


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"[transform] ERROR: {exc}", file=sys.stderr)
        import traceback
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)
