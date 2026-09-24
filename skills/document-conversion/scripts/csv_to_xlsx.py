"""Build an Excel workbook from CSV files, one sheet per CSV.

Usage:
    python csv_to_xlsx.py <csv or folder> [more ...] -o <book.xlsx> [--base original.xlsx] [--infer-types] [--formulas]

Sheets are named after the CSV files, with a leading NN- order prefix (as written by xlsx_to_csv.py) removed.
A folder contributes every *.csv in it, in name order.

New workbook (no --base): header row bold and shaded, frozen, with filters, and columns sized to their content.
Every cell is text unless --infer-types, which turns plain integers, decimals, ISO dates and TRUE/FALSE into typed
cells. Integers with a leading zero stay text, so IDs and codes keep their zeros.

--base original.xlsx: the edited values are written into a copy of the original, so its formatting, column
widths, data validation, conditional formatting and untouched sheets are kept. A cell keeps its original type
when the new text still parses as that type. A formula is kept while the CSV still shows its calculated value;
an edited value replaces it. Rows and columns the CSV no longer has are cleared. openpyxl drops charts, images
and shapes from the base; the script warns when the base has any.

Text starting with "=" is written as text unless --formulas, which writes it as a formula.
Needs the Python package openpyxl.
"""
import argparse
import csv
import datetime
import re
import sys
import zipfile
from pathlib import Path

from convert_common import require_module
from xlsx_to_csv import INVALID_FILE_CHARS, cell_text

ORDER_PREFIX = re.compile(r"^\d{2,}-")
INVALID_SHEET_CHARS = re.compile(r"[\[\]:*?/\\]")
INTEGER = re.compile(r"^-?(0|[1-9]\d{0,14})$")
DECIMAL = re.compile(r"^-?(0|[1-9]\d*)\.\d+$")
DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
DATETIME = re.compile(r"^\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}(:\d{2}(\.\d+)?)?$")


def sheet_name(csv_path):
    return INVALID_SHEET_CHARS.sub("_", ORDER_PREFIX.sub("", csv_path.stem))[:31] or "Sheet"


def infer(text):
    if INTEGER.match(text):
        return int(text)
    if DECIMAL.match(text):
        return float(text)
    if DATE.match(text):
        try:
            return datetime.datetime.strptime(text, "%Y-%m-%d")
        except ValueError:
            return text
    if DATETIME.match(text):
        try:
            return datetime.datetime.fromisoformat(text)
        except ValueError:
            return text
    if text.upper() in ("TRUE", "FALSE"):
        return text.upper() == "TRUE"
    return text


def coerce_like(text, original):
    """Parse text as the original cell's type when it still fits; None when it does not."""
    if isinstance(original, bool):
        return text.upper() == "TRUE" if text.upper() in ("TRUE", "FALSE") else None
    if isinstance(original, (int, float)):
        try:
            number = float(text)
        except ValueError:
            return None
        return int(number) if isinstance(original, int) and number.is_integer() else number
    if isinstance(original, (datetime.datetime, datetime.date)):
        try:
            return datetime.datetime.fromisoformat(text)
        except ValueError:
            return None
    return None


def set_cell(cell, text, original, infer_types, formulas):
    if text == "":
        cell.value = None
        return
    if text.startswith("="):
        cell.value = text
        if not formulas:
            cell.data_type = "s"
        return
    typed = coerce_like(text, original)
    cell.value = typed if typed is not None else (infer(text) if infer_types else text)
    if original is None and isinstance(cell.value, datetime.datetime) and cell.value.time() == datetime.time(0):
        cell.number_format = "yyyy-mm-dd"


def read_csv(path):
    with open(path, encoding="utf-8-sig", newline="") as f:
        return list(csv.reader(f))


def style_new_sheet(ws, openpyxl):
    if ws.max_row == 1 and ws.max_column == 1 and ws.cell(1, 1).value is None:
        return
    header_font = openpyxl.styles.Font(bold=True)
    header_fill = openpyxl.styles.PatternFill("solid", fgColor="DCE6F1")
    for cell in ws[1]:
        cell.font, cell.fill = header_font, header_fill
    ws.freeze_panes = "A2"
    ws.auto_filter.ref = ws.dimensions
    for column in ws.columns:
        longest = max((len(cell_text(c.value)) for c in column), default=0)
        ws.column_dimensions[column[0].column_letter].width = min(60, max(8, longest + 2))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("inputs", type=Path, nargs="+", help="CSV files and/or folders of CSV files")
    parser.add_argument("-o", "--output", type=Path, required=True, help="workbook to write")
    parser.add_argument("--base", type=Path, help="original workbook to write the values into (it is not modified)")
    parser.add_argument("--infer-types", action="store_true", help="type plain numbers, ISO dates and TRUE/FALSE")
    parser.add_argument("--formulas", action="store_true", help='write text starting with "=" as formulas')
    a = parser.parse_args()
    openpyxl = require_module("openpyxl")

    csvs = []
    for item in a.inputs:
        item = item.resolve()
        if item.is_dir():
            csvs += sorted(item.glob("*.csv"))
        elif item.is_file():
            csvs.append(item)
        else:
            sys.exit(f"not found: {item}")
    if not csvs:
        sys.exit("no CSV files given")
    names = [sheet_name(p) for p in csvs]
    duplicates = {n for n in names if names.count(n) > 1}
    if duplicates:
        sys.exit(f"more than one CSV maps to sheet name(s): {', '.join(sorted(duplicates))}")

    out = a.output.resolve()
    if a.base:
        base = a.base.resolve()
        if base == out:
            sys.exit("--base and -o are the same file; write to a new file so the original stays intact")
        with zipfile.ZipFile(base) as z:
            lost = sorted({n.split("/")[1] for n in z.namelist() if n.startswith(("xl/charts/", "xl/drawings/", "xl/media/"))})
        if lost:
            print(f"  warning: the base has {', '.join(lost)}; openpyxl does not keep them, so the output will not have them")
        book = openpyxl.load_workbook(base)
        cached = openpyxl.load_workbook(base, data_only=True)
        # A sheet name with characters Windows forbids in file names came back from xlsx_to_csv.py with "_".
        by_file_name = {INVALID_FILE_CHARS.sub("_", title): title for title in book.sheetnames}
        names = [n if n in book.sheetnames else by_file_name.get(n, n) for n in names]
    else:
        book = openpyxl.Workbook()
        book.remove(book.active)
        cached = None

    print(f"Building {out.name}")
    for path, name in zip(csvs, names):
        rows = read_csv(path)
        existing = name in book.sheetnames
        ws = book[name] if existing else book.create_sheet(name)
        kept = replaced = 0
        for r, row in enumerate(rows, 1):
            for c, text in enumerate(row, 1):
                cell = ws.cell(r, c)
                if existing and cell.data_type == "f":
                    if text in (cell_text(cached[name].cell(r, c).value), cell.value):
                        kept += 1
                        continue
                    replaced += 1
                set_cell(cell, text, cell.value if existing else None, a.infer_types, a.formulas)
        cleared = 0
        if existing:
            width = max((len(r) for r in rows), default=0)
            for row in ws.iter_rows():
                for cell in row:
                    if (cell.row > len(rows) or cell.column > width) and cell.value is not None:
                        cell.value = None
                        cleared += 1
        else:
            style_new_sheet(ws, openpyxl)
        notes = [f"{n} {label}" for n, label in ((kept, "formula(s) kept"), (replaced, "formula(s) replaced by edited values"),
                                                   (cleared, "cell(s) cleared")) if n]
        print(f"  {name} ({'updated' if existing else 'new'}): {len(rows)} row(s)" + (f" - {'; '.join(notes)}" if notes else ""))

    out.parent.mkdir(parents=True, exist_ok=True)
    book.save(out)
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
