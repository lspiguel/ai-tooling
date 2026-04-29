"""Custom Jinja2 filters for D365 Context Exporter templates."""

import re
from datetime import datetime

COMPONENT_TYPES = {
    1: "Table (Entity)",
    3: "Column (Attribute)",
    5: "Relationship",
    20: "Option Set (Global)",
    50: "Role",
    60: "Form",
    90: "Model-driven App",
    91: "Canvas App",
    92: "Cloud Flow",
}


def _component_type_name(value: int) -> str:
    return COMPONENT_TYPES.get(value, f"Unknown ({value})")


def _schemaname_to_title(value: str) -> str:
    if not value:
        return value
    # Split on underscores and camelCase boundaries, then title-case each word.
    s = re.sub(r"([A-Z])", r" \1", value)
    words = re.split(r"[_\s]+", s)
    return " ".join(w.capitalize() for w in words if w)


def _markdown_table(rows: list, columns: list) -> str:
    if not rows:
        return "_No data._"
    header = "| " + " | ".join(columns) + " |"
    separator = "| " + " | ".join("---" for _ in columns) + " |"
    lines = [header, separator]
    for row in rows:
        cells = [str(row.get(col, "") if isinstance(row, dict) else getattr(row, col, "")) for col in columns]
        lines.append("| " + " | ".join(cells) + " |")
    return "\n".join(lines)


def _csv_list(items: list, attr: str = None) -> str:
    if not items:
        return ""
    if attr:
        return ", ".join(str(item.get(attr, "") if isinstance(item, dict) else getattr(item, attr, "")) for item in items)
    return ", ".join(str(item) for item in items)


def _optionset_label(options: list, value: int) -> str:
    for opt in options or []:
        if isinstance(opt, dict) and opt.get("Value") == value:
            label = opt.get("Label", {})
            if isinstance(label, dict):
                return label.get("UserLocalizedLabel", {}).get("Label", str(value))
            return str(label)
    return str(value)


def _iso_date(value: str) -> str:
    if not value:
        return value
    try:
        dt = datetime.fromisoformat(value.replace("Z", "+00:00"))
        return dt.strftime("%Y-%m-%d")
    except (ValueError, AttributeError):
        return value[:10] if len(value) >= 10 else value


def get_filters() -> dict:
    """Return a dict of filter_name → callable for registration with a Jinja2 Environment."""
    return {
        "component_type_name": _component_type_name,
        "schemaname_to_title": _schemaname_to_title,
        "markdown_table": _markdown_table,
        "csv_list": _csv_list,
        "optionset_label": _optionset_label,
        "iso_date": _iso_date,
    }
