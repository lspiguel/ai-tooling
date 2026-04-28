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
    # Add more as needed
}

def component_type_name(component_type):
    return COMPONENT_TYPES.get(component_type, f"Unknown ({component_type})")