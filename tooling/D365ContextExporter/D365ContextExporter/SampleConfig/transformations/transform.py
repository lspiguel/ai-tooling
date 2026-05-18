"""D365 Context Exporter — Jinja2 template orchestrator."""

import json
import os
import sys

try:
    from jinja2 import Environment, FileSystemLoader, StrictUndefined
    import filters as _filters_module
except ImportError as e:
    print(f"[transform] Import error: {e}", file=sys.stderr)
    print(
        "[transform] Ensure pylibs.zip is extracted and on sys.path, "
        "or activate a venv with Jinja2 installed.",
        file=sys.stderr,
    )
    sys.exit(1)


def run(input_path, template_path, out_dir, spec_name):
    with open(input_path, encoding="utf-8") as f:
        context = json.load(f)

    context["_spec"] = spec_name

    template_dir = os.path.dirname(os.path.abspath(template_path))
    template_name = os.path.basename(template_path)

    env = Environment(
        loader=FileSystemLoader(template_dir),
        undefined=StrictUndefined,
        keep_trailing_newline=True,
    )
    env.filters.update(_filters_module.get_filters())

    template = env.get_template(template_name)
    rendered = template.render(**context)

    output_path = os.path.join(out_dir, "output.md")
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(rendered)

    print(f"[transform] output.md written ({len(rendered.encode('utf-8'))} bytes)")


# Embedded mode: C# host injects these four variables into the module scope before execution.
if "input_path" in globals():
    run(input_path, template, out_dir, spec)  # noqa: F821
elif __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(
        description="Render a Jinja2 template from intermediate.json."
    )
    parser.add_argument("--input",    required=True, help="Path to intermediate.json")
    parser.add_argument("--template", required=True, help="Path to the .j2 template file")
    parser.add_argument("--out",      required=True, help="Output directory for output.md")
    parser.add_argument("--spec",     required=True, help="Spec name injected as _spec")
    args = parser.parse_args()
    try:
        run(args.input, args.template, args.out, args.spec)
    except Exception as exc:
        print(f"[transform] ERROR: {exc}", file=sys.stderr)
        import traceback
        traceback.print_exc(file=sys.stderr)
        sys.exit(1)
