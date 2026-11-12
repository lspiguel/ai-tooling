"""Split an Excel workbook into one CSV per sheet, so an agent can read and edit the data.

Usage:
    python xlsx_to_csv.py <book.xlsx> [-o folder] [--formulas] [--force]

Writes <stem>-sheets/NN-<sheet name>.csv beside the workbook (UTF-8, comma-separated). The NN prefix keeps the
sheet order; csv_to_xlsx.py strips it again. Cells are written as Excel last calculated them; --formulas writes
the formula text (=SUM(B2:B9)) instead. Dates are written as ISO 8601. Trailing empty rows and columns are dropped.

Lost on the way out, and reported: formatting, merged-cell layout (the value stays in the top-left cell),
comments, data validation, charts and images. To keep those, write the edited CSVs back into a copy of the
original workbook with csv_to_xlsx.py --base.
Needs the Python package openpyxl.
"""
import argparse
import csv
import datetime
import re
import sys
from pathlib import Path

from convert_common import require_module

INVALID_FILE_CHARS = re.compile(r'[\\/:*?"<>|]')


def cell_text(value):
    if value is None:
        return ""
    if isinstance(value, bool):
        return "TRUE" if value else "FALSE"
    if isinstance(value, datetime.datetime):
        return value.date().isoformat() if value.time() == datetime.time(0) else value.isoformat(sep=" ")
    if isinstance(value, (datetime.date, datetime.time)):
        return value.isoformat()
    if isinstance(value, float):
        return format(value, ".15g")
    return str(value)


def sheet_rows(ws):
    rows = [[cell_text(v) for v in row] for row in ws.iter_rows(values_only=True)]
    while rows and not any(rows[-1]):
        rows.pop()
    width = max((max((i + 1 for i, v in enumerate(r) if v), default=0) for r in rows), default=0)
    return [r[:width] + [""] * (width - len(r)) for r in rows]


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("workbook", type=Path)
    parser.add_argument("-o", "--output", type=Path, help="folder to write (default: <stem>-sheets beside the workbook)")
    parser.add_argument("--formulas", action="store_true", help="write formulas instead of their calculated values")
    parser.add_argument("--force", action="store_true", help="replace CSVs already in the output folder")
    a = parser.parse_args()
    openpyxl = require_module("openpyxl")

    src = a.workbook.resolve()
    if not src.is_file():
        sys.exit(f"not found: {src}")
    out = (a.output or src.with_name(f"{src.stem}-sheets")).resolve()
    existing = sorted(out.glob("*.csv")) if out.exists() else []
    if existing and not a.force:
        sys.exit(f"{out} already holds {len(existing)} CSV file(s); pass --force to replace them")
    for old in existing:
        old.unlink()
    out.mkdir(parents=True, exist_ok=True)

    # openpyxl reads either the formulas or their last calculated values, never both at once.
    formulas = openpyxl.load_workbook(src, data_only=False)
    values = openpyxl.load_workbook(src, data_only=True)
    book = formulas if a.formulas else values
    print(f"Splitting {src.name}")
    for index, ws in enumerate(book.worksheets, 1):
        rows = sheet_rows(ws)
        name = f"{index:02d}-{INVALID_FILE_CHARS.sub('_', ws.title)}.csv"
        with open(out / name, "w", encoding="utf-8", newline="") as f:
            csv.writer(f).writerows(rows)

        notes = []
        if ws.sheet_state != "visible":
            notes.append(ws.sheet_state)
        if ws.merged_cells.ranges:
            notes.append(f"{len(ws.merged_cells.ranges)} merged range(s)")
        if ws.data_validations.dataValidation:
            notes.append("data validation")
        if getattr(ws, "_charts", None) or getattr(ws, "_images", None):
            notes.append("charts/images")
        formula_cells = [c.coordinate for row in formulas[ws.title].iter_rows() for c in row if c.data_type == "f"]
        if formula_cells and not a.formulas:
            notes.append(f"{len(formula_cells)} formula(s) written as values")
            uncached = sum(1 for c in formula_cells if values[ws.title][c].value is None)
            if uncached:
                notes.append(f"{uncached} never calculated, so empty - open and save in Excel, or use --formulas")
        print(f"  {name}: {len(rows)} row(s) x {len(rows[0]) if rows else 0} column(s)" + (f" - {'; '.join(notes)}" if notes else ""))
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
