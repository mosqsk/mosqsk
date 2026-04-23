"""Parse car data from PDF, CSV, and XLS/XLSX files."""
from __future__ import annotations

import os
import re
from pathlib import Path

from scrapers.base import (
    CarListing,
    extract_fuel,
    extract_mileage,
    extract_power,
    extract_price,
    extract_transmission,
    extract_year,
)

SUPPORTED_EXTENSIONS = {".pdf", ".csv", ".xls", ".xlsx"}

# Common column name mappings (Croatian and English)
COLUMN_MAP = {
    "make": ["marka", "make", "brand", "proizvođač", "proizvodzac"],
    "model": ["model", "tip", "type"],
    "year": ["godina", "year", "god", "yr", "godište", "godiste"],
    "price_eur": ["cijena", "price", "cena", "cijenaeur", "priceeur", "eur", "€"],
    "mileage_km": ["kilometraža", "mileage", "km", "kilometraza", "prijeđeno", "prijeden"],
    "fuel_type": ["gorivo", "fuel", "vrsta goriva"],
    "transmission": ["mjenjač", "transmisija", "transmission", "mjenjac"],
    "power_kw": ["snaga", "kw", "power", "snaga kw"],
    "color": ["boja", "color", "colour"],
    "body_type": ["karoserija", "body", "tip karoserije"],
}


def _normalize_col(name: str) -> str:
    return re.sub(r"[^a-z0-9]", "", name.lower().strip())


def _map_columns(columns: list[str]) -> dict[str, str]:
    """Return {field_name: actual_column_name} mapping."""
    mapping: dict[str, str] = {}
    norm_cols = {_normalize_col(c): c for c in columns}
    for field, aliases in COLUMN_MAP.items():
        for alias in aliases:
            norm_alias = _normalize_col(alias)
            if norm_alias in norm_cols:
                mapping[field] = norm_cols[norm_alias]
                break
    return mapping


def parse_file(path: str | Path) -> list[CarListing]:
    path = Path(path)
    suffix = path.suffix.lower()
    if suffix == ".pdf":
        return _parse_pdf(path)
    if suffix == ".csv":
        return _parse_csv(path)
    if suffix in (".xls", ".xlsx"):
        return _parse_excel(path)
    print(f"  WARNING: Unsupported file type: {path}")
    return []


def parse_folder(folder: str | Path) -> list[CarListing]:
    folder = Path(folder)
    all_listings: list[CarListing] = []
    files = sorted(
        f for f in folder.rglob("*") if f.suffix.lower() in SUPPORTED_EXTENSIONS
    )
    if not files:
        print(f"  WARNING: No supported files found in {folder}")
        return []
    for f in files:
        print(f"  Parsing file: {f}")
        listings = parse_file(f)
        print(f"  Found {len(listings)} listings in {f.name}")
        all_listings.extend(listings)
    return all_listings


# ── PDF ──────────────────────────────────────────────────────────────────────

def _parse_pdf(path: Path) -> list[CarListing]:
    try:
        import pdfplumber
    except ImportError:
        print("  ERROR: pdfplumber not installed. Run: pip install pdfplumber")
        return []

    listings: list[CarListing] = []
    source = str(path)

    with pdfplumber.open(path) as pdf:
        for page in pdf.pages:
            # Try extracting tables first (structured PDFs from dealers)
            tables = page.extract_tables()
            for table in tables:
                if not table:
                    continue
                headers = [str(c or "").strip() for c in table[0]]
                col_map = _map_columns(headers)
                if not col_map:
                    continue
                for row in table[1:]:
                    row_dict = {headers[i]: str(v or "").strip() for i, v in enumerate(row)}
                    listing = _row_to_listing(row_dict, col_map, source, "pdf")
                    if listing.make or listing.model or listing.price_eur:
                        listings.append(listing)

            # If no structured table data, fall back to text extraction
            if not listings:
                text = page.extract_text() or ""
                listings.extend(_text_to_listings(text, source, "pdf"))

    return listings


# ── CSV ───────────────────────────────────────────────────────────────────────

def _parse_csv(path: Path) -> list[CarListing]:
    try:
        import pandas as pd
    except ImportError:
        print("  ERROR: pandas not installed. Run: pip install pandas")
        return []

    listings: list[CarListing] = []
    source = str(path)

    # Try common separators
    for sep in (",", ";", "\t"):
        try:
            df = pd.read_csv(path, sep=sep, dtype=str, on_bad_lines="skip")
            if df.shape[1] > 1:
                break
        except Exception:
            continue
    else:
        print(f"  WARNING: Could not parse CSV {path}")
        return []

    df.columns = [str(c).strip() for c in df.columns]
    col_map = _map_columns(list(df.columns))

    for _, row in df.iterrows():
        row_dict = row.to_dict()
        listing = _row_to_listing(row_dict, col_map, source, "csv")
        if listing.make or listing.model or listing.price_eur:
            listings.append(listing)

    # If no column mapping worked, try heuristic per-row text scan
    if not listings:
        for _, row in df.iterrows():
            text = " ".join(str(v) for v in row.values if v)
            ls = _text_to_listings(text, source, "csv")
            listings.extend(ls)

    return listings


# ── XLS/XLSX ─────────────────────────────────────────────────────────────────

def _parse_excel(path: Path) -> list[CarListing]:
    try:
        import pandas as pd
    except ImportError:
        print("  ERROR: pandas not installed. Run: pip install pandas openpyxl")
        return []

    listings: list[CarListing] = []
    source = str(path)

    try:
        engine = "openpyxl" if path.suffix.lower() == ".xlsx" else "xlrd"
        xl = pd.ExcelFile(path, engine=engine)
    except Exception as exc:
        print(f"  WARNING: Could not open {path}: {exc}")
        return []

    for sheet in xl.sheet_names:
        try:
            df = xl.parse(sheet, dtype=str)
        except Exception:
            continue
        if df.empty:
            continue

        df.columns = [str(c).strip() for c in df.columns]
        col_map = _map_columns(list(df.columns))

        sheet_listings = []
        for _, row in df.iterrows():
            row_dict = row.to_dict()
            listing = _row_to_listing(row_dict, col_map, source, "excel")
            listing.description = f"Sheet: {sheet}"
            if listing.make or listing.model or listing.price_eur:
                sheet_listings.append(listing)

        if not sheet_listings:
            for _, row in df.iterrows():
                text = " ".join(str(v) for v in row.values if v and str(v) != "nan")
                ls = _text_to_listings(text, source, "excel")
                sheet_listings.extend(ls)

        listings.extend(sheet_listings)

    return listings


# ── Helpers ──────────────────────────────────────────────────────────────────

def _row_to_listing(
    row: dict,
    col_map: dict[str, str],
    source: str,
    source_type: str,
) -> CarListing:
    def get(field: str) -> str:
        col = col_map.get(field)
        if col and col in row:
            val = str(row[col]).strip()
            return "" if val.lower() in ("nan", "none", "") else val
        return ""

    listing = CarListing(source_url=source, source_type=source_type)
    listing.make = get("make")
    listing.model = get("model")
    listing.year = get("year") or extract_year(get("model"))
    listing.price_eur = get("price_eur") or extract_price(get("price_eur"))
    listing.mileage_km = get("mileage_km")
    listing.fuel_type = get("fuel_type")
    listing.transmission = get("transmission")
    listing.power_kw = get("power_kw")
    listing.color = get("color")
    listing.body_type = get("body_type")

    # Normalize price (strip currency symbols, convert Croatian numbers)
    if listing.price_eur:
        listing.price_eur = extract_price(listing.price_eur) or listing.price_eur

    return listing


def _text_to_listings(text: str, source: str, source_type: str) -> list[CarListing]:
    """Extract listings from raw text using regex heuristics."""
    listings = []
    price = extract_price(text)
    if price:
        listing = CarListing(source_url=source, source_type=source_type)
        listing.price_eur = price
        listing.year = extract_year(text)
        listing.mileage_km = extract_mileage(text)
        listing.fuel_type = extract_fuel(text)
        listing.transmission = extract_transmission(text)
        listing.power_kw = extract_power(text)
        listings.append(listing)
    return listings
