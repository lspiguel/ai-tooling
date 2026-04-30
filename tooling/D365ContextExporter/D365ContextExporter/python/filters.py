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

ATTR_TYPE_ABBREV = {
    "String": "str",
    "Memo": "text",
    "Lookup": "lkp",
    "Picklist": "opt",
    "MultiSelectPicklist": "multiopt",
    "Boolean": "bool",
    "Integer": "int",
    "Decimal": "dec",
    "Money": "money",
    "DateTime": "dt",
    "Image": "img",
    "Uniqueidentifier": "pk",
    "File": "file",
    "Owner": "lkp",
    "Customer": "lkp",
    "EntityName": "str",
    "BigInt": "int",
    "Double": "dec",
    "State": "opt",
    "Status": "opt",
    "Virtual": "-",
    "ManagedProperty": "-",
    "CalendarRules": "-",
    "PartyList": "lkp",
}

FORM_TYPE_SHORT = {
    7: "quick",
    8: "main",
    11: "card",
    106: "quickCreate",
}

PLUGIN_STAGE = {
    10: "PreVal",
    20: "Pre",
    40: "Post",
    45: "Post",
}

PLUGIN_MODE = {
    0: "Sync",
    1: "Async",
}

ENVVAR_TYPE = {
    100000000: "String",
    100000001: "Number",
    100000002: "Boolean",
    100000003: "JSON",
    100000004: "DataSource",
    100000005: "Secret",
}

API_PARAM_TYPE = {
    0: "bool",
    1: "dt",
    2: "dec",
    3: "Entity",
    4: "EntityCollection",
    5: "EntityReference",
    6: "float",
    7: "int",
    8: "money",
    9: "opt",
    10: "str",
    11: "str[]",
    12: "guid",
}


def _component_type_name(value: int) -> str:
    return COMPONENT_TYPES.get(value, f"Unknown ({value})")


def _schemaname_to_title(value: str) -> str:
    if not value:
        return value
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


def _attr_type_abbrev(value) -> str:
    """Map Dataverse AttributeType string to a short abbreviation."""
    if not value:
        return "-"
    return ATTR_TYPE_ABBREV.get(str(value), str(value))


def _req_indicator(value) -> str:
    """Map RequiredLevel.Value string to bold/plain/dash indicator."""
    if not value:
        return "-"
    v = str(value)
    if v == "Required":
        return "**R**"
    if v == "Recommended":
        return "r"
    return "-"


def _format_forms(forms) -> str:
    """Group forms by Name, append abbreviated type list in parens.

    e.g. [{Name:"Information",Type:8},{Name:"Information",Type:11}] → "Information(card,main)"
    """
    groups: dict = {}
    for form in forms or []:
        name = form.get("Name", "") if isinstance(form, dict) else getattr(form, "Name", "")
        ftype = form.get("Type", 0) if isinstance(form, dict) else getattr(form, "Type", 0)
        if not name:
            continue
        short = FORM_TYPE_SHORT.get(int(ftype) if ftype is not None else 0, str(ftype))
        if name not in groups:
            groups[name] = []
        if short not in groups[name]:
            groups[name].append(short)
    parts = [f"{name}({','.join(groups[name])})" for name in groups]
    return ", ".join(parts) if parts else "—"


def _format_views(views) -> str:
    """Return comma-separated view names, excluding personal/system view types."""
    SKIP_TYPES = {16, 32, 64, 128, 256, 512, 1024, 4096, 16384}
    names: list = []
    for v in views or []:
        qtype = v.get("QueryType", -1) if isinstance(v, dict) else getattr(v, "QueryType", -1)
        name = v.get("Name", "") if isinstance(v, dict) else getattr(v, "Name", "")
        try:
            qt_int = int(qtype)
        except (TypeError, ValueError):
            qt_int = -1
        if name and qt_int not in SKIP_TYPES and name not in names:
            names.append(name)
    return ", ".join(names) if names else "—"


def _plugin_stage(value) -> str:
    if value is None:
        return "—"
    try:
        return PLUGIN_STAGE.get(int(value), str(value))
    except (TypeError, ValueError):
        return str(value)


def _plugin_mode(value) -> str:
    if value is None:
        return "—"
    try:
        return PLUGIN_MODE.get(int(value), str(value))
    except (TypeError, ValueError):
        return str(value)


def _flow_trigger(name: str) -> str:
    """Infer cloud flow trigger category from the flow name."""
    nl = (name or "").lower()
    if "http" in nl:
        return "HTTP"
    if any(x in nl for x in ["sched", "every 1 day", "every weekend", "daily", "weekly", "monthly", "annual", "dec 31", "every 1 week"]):
        return "Sched"
    if "child" in nl or "childprocess" in nl:
        return "Child"
    if any(x in nl for x in ["on demand", "ondemand", "manual", "manuallybatch", "on-demand"]):
        return "Manual"
    return "Auto"


def _classic_trigger(wf) -> str:
    """Derive classic workflow trigger from boolean flags."""
    triggers = []
    data = wf if isinstance(wf, dict) else {}
    if data.get("triggeroncreate"):
        triggers.append("Create")
    if data.get("triggeronupdateattributelist"):
        triggers.append("Update")
    if data.get("triggerondelete"):
        triggers.append("Delete")
    if not triggers:
        triggers.append("Manual")
    return "/".join(triggers)


def _envvar_type(value) -> str:
    if value is None:
        return "—"
    try:
        return ENVVAR_TYPE.get(int(value), str(value))
    except (TypeError, ValueError):
        return str(value)


def _api_param_type(value) -> str:
    if value is None:
        return "?"
    try:
        return API_PARAM_TYPE.get(int(value), str(value))
    except (TypeError, ValueError):
        return str(value)


def _pluck(items, key: str) -> list:
    """Extract values for a given key (including dot-in-key strings) from a list of dicts."""
    result = []
    seen = set()
    for item in items or []:
        val = item.get(key) if isinstance(item, dict) else getattr(item, key, None)
        if val is not None and val not in seen:
            seen.add(val)
            result.append(val)
    return result


def _group_by_key(items, key: str) -> list:
    """Group a list of dicts by a key string (supports literal-dot keys like 'a.b').

    Returns list of (group_value, [items]) tuples, preserving insertion order.
    """
    groups: dict = {}
    order: list = []
    for item in items or []:
        val = item.get(key) if isinstance(item, dict) else getattr(item, key, None)
        if val not in groups:
            groups[val] = []
            order.append(val)
        groups[val].append(item)
    return [(k, groups[k]) for k in order]


def _display_label(label_obj, fallback: str = "—") -> str:
    """Safely extract UserLocalizedLabel.Label from a D365 Label object."""
    if not label_obj:
        return fallback
    if isinstance(label_obj, dict):
        usl = label_obj.get("UserLocalizedLabel")
        if isinstance(usl, dict):
            return usl.get("Label", fallback) or fallback
    return fallback


def get_filters() -> dict:
    """Return a dict of filter_name → callable for registration with a Jinja2 Environment."""
    return {
        "component_type_name": _component_type_name,
        "schemaname_to_title": _schemaname_to_title,
        "markdown_table": _markdown_table,
        "csv_list": _csv_list,
        "optionset_label": _optionset_label,
        "iso_date": _iso_date,
        "attr_type_abbrev": _attr_type_abbrev,
        "req_indicator": _req_indicator,
        "format_forms": _format_forms,
        "format_views": _format_views,
        "plugin_stage": _plugin_stage,
        "plugin_mode": _plugin_mode,
        "flow_trigger": _flow_trigger,
        "classic_trigger": _classic_trigger,
        "envvar_type": _envvar_type,
        "api_param_type": _api_param_type,
        "pluck": _pluck,
        "group_by_key": _group_by_key,
        "display_label": _display_label,
    }
