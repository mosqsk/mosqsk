from __future__ import annotations

import json
import re
from dataclasses import dataclass, field, asdict
from datetime import datetime
from typing import Any

from bs4 import BeautifulSoup

from config import (
    PRICE_RE, YEAR_RE, MILEAGE_RE, KW_RE, HP_RE,
    FUEL_KEYWORDS, TRANSMISSION_KEYWORDS,
    BRAND_RE, CAR_BRANDS,
    parse_croatian_number,
)


@dataclass
class CarListing:
    source_url: str = ""
    source_type: str = ""
    name: str = ""
    make: str = ""
    model: str = ""
    year: str = ""
    price_eur: str = ""
    mileage_km: str = ""
    fuel_type: str = ""
    transmission: str = ""
    power_kw: str = ""
    body_type: str = ""
    color: str = ""
    listing_url: str = ""
    description: str = ""
    scraped_at: str = field(default_factory=lambda: datetime.now().strftime("%Y-%m-%d %H:%M:%S"))

    def to_dict(self) -> dict:
        return asdict(self)


def extract_price(text: str) -> str:
    match = PRICE_RE.search(text)
    if match:
        val = parse_croatian_number(match.group(1))
        if val:
            # Convert HRK to EUR if needed (rough 7.53 rate, but just store as-is with currency note)
            currency = match.group(0).upper()
            if "KN" in currency or "HRK" in currency:
                return str(round(val / 7.53, 0))
            return str(int(val))
    return ""


def extract_year(text: str) -> str:
    match = YEAR_RE.search(text)
    return match.group(1) if match else ""


def extract_mileage(text: str) -> str:
    match = MILEAGE_RE.search(text)
    if match:
        val = parse_croatian_number(match.group(1))
        return str(int(val)) if val else ""
    return ""


def extract_power(text: str) -> str:
    kw = KW_RE.search(text)
    if kw:
        return kw.group(1)
    hp = HP_RE.search(text)
    if hp:
        return str(round(int(hp.group(1)) * 0.7355))
    return ""


def extract_fuel(text: str) -> str:
    lower = text.lower()
    for fuel, keywords in FUEL_KEYWORDS.items():
        if any(kw in lower for kw in keywords):
            return fuel
    return ""


def extract_transmission(text: str) -> str:
    lower = text.lower()
    for trans, keywords in TRANSMISSION_KEYWORDS.items():
        if any(kw in lower for kw in keywords):
            return trans
    return ""


def extract_make_model(text: str) -> tuple[str, str, str]:
    """Return (name, make, model) extracted from a text chunk.

    Scans for a known brand name, then takes the words that follow it
    as the model. Returns the full matched string as name.
    """
    match = BRAND_RE.search(text)
    if not match:
        return "", "", ""

    make = match.group(1)
    # Normalise casing (e.g. "VOLKSWAGEN" → "Volkswagen")
    for brand in CAR_BRANDS:
        if brand.lower() == make.lower():
            make = brand
            break

    # Grab up to 5 words after the brand as the model
    after = text[match.end():].strip()
    model_words = after.split()[:5]
    # Stop at common non-model tokens
    stop = {"€", "eur", "kn", "hrk", "km", "god", "automatik", "manual"}
    trimmed = []
    for w in model_words:
        if w.lower() in stop or re.match(r"^\d{4,}$", w):
            break
        trimmed.append(w)
    model = " ".join(trimmed)
    name = (make + " " + model).strip()
    return name, make, model


def extract_from_jsonld(soup: BeautifulSoup) -> list[dict]:
    results = []
    for tag in soup.find_all("script", type="application/ld+json"):
        try:
            data = json.loads(tag.string or "")
        except (json.JSONDecodeError, TypeError):
            continue
        if isinstance(data, list):
            for item in data:
                if isinstance(item, dict) and item.get("@type") in ("Car", "Vehicle", "Product"):
                    results.append(item)
        elif isinstance(data, dict):
            if data.get("@type") in ("Car", "Vehicle", "Product"):
                results.append(data)
            # ItemList
            items = data.get("itemListElement", [])
            for item in items:
                if isinstance(item, dict) and item.get("@type") in ("Car", "Vehicle", "Product"):
                    results.append(item)
    return results


def jsonld_to_listing(item: dict, source_url: str) -> CarListing:
    listing = CarListing(source_url=source_url, source_type="web")
    listing.make = item.get("brand", {}).get("name", "") if isinstance(item.get("brand"), dict) else item.get("brand", "")
    listing.model = item.get("model", "")
    listing.name = item.get("name", f"{listing.make} {listing.model}".strip())
    listing.year = str(item.get("vehicleModelDate", item.get("datePosted", "")))[:4]
    listing.mileage_km = str(item.get("mileageFromOdometer", {}).get("value", "")) if isinstance(item.get("mileageFromOdometer"), dict) else ""
    listing.fuel_type = item.get("fuelType", "")
    listing.transmission = item.get("vehicleTransmission", "")
    listing.color = item.get("color", "")
    offers = item.get("offers", {})
    if isinstance(offers, dict):
        listing.price_eur = str(offers.get("price", ""))
    listing.listing_url = item.get("url", source_url)
    return listing


def extract_embedded_json(html: str) -> list[dict[str, Any]]:
    """Try to find car data embedded in JS variables (Next.js, Nuxt, custom)."""
    results = []
    patterns = [
        r"window\.__INITIAL_STATE__\s*=\s*(\{.*?\});",
        r"window\.__data__\s*=\s*(\{.*?\});",
        r"__NEXT_DATA__['\"]?\s*=\s*(\{.*?\})\s*</script>",
        r"window\.vehicleData\s*=\s*(\[.*?\]);",
    ]
    for pattern in patterns:
        match = re.search(pattern, html, re.DOTALL)
        if match:
            try:
                data = json.loads(match.group(1))
                if isinstance(data, (dict, list)):
                    results.append(data if isinstance(data, dict) else {"items": data})
            except json.JSONDecodeError:
                continue
    return results
