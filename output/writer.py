"""Write CarListing results to a CSV file."""
from __future__ import annotations

import csv
from datetime import datetime
from pathlib import Path

from scrapers.base import CarListing

FIELDNAMES = [
    "source_type",
    "make",
    "model",
    "year",
    "price_eur",
    "mileage_km",
    "fuel_type",
    "transmission",
    "power_kw",
    "body_type",
    "color",
    "listing_url",
    "source_url",
    "description",
    "scraped_at",
]

DISPLAY_HEADERS = {
    "source_type": "Source",
    "make": "Make",
    "model": "Model",
    "year": "Year",
    "price_eur": "Price (EUR)",
    "mileage_km": "Mileage (km)",
    "fuel_type": "Fuel",
    "transmission": "Transmission",
    "power_kw": "Power (kW)",
    "body_type": "Body Type",
    "color": "Color",
    "listing_url": "Listing URL",
    "source_url": "Source URL",
    "description": "Notes",
    "scraped_at": "Scraped At",
}


def default_output_path() -> str:
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    return f"car_offers_{timestamp}.csv"


def write_csv(listings: list[CarListing], output_path: str | None = None) -> str:
    if not output_path:
        output_path = default_output_path()

    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)

    with open(path, "w", newline="", encoding="utf-8-sig") as f:
        # utf-8-sig adds BOM so Excel opens it correctly with Croatian characters
        writer = csv.DictWriter(
            f,
            fieldnames=FIELDNAMES,
            extrasaction="ignore",
            quoting=csv.QUOTE_ALL,
        )
        # Write human-readable headers
        writer.writerow(DISPLAY_HEADERS)
        for listing in listings:
            writer.writerow(listing.to_dict())

    return str(path)


def print_summary(listings: list[CarListing], output_path: str) -> None:
    total = len(listings)
    by_source = {}
    for l in listings:
        by_source[l.source_type] = by_source.get(l.source_type, 0) + 1

    print(f"\n{'=' * 50}")
    print(f"  RESULTS SUMMARY")
    print(f"{'=' * 50}")
    print(f"  Total listings found : {total}")
    for src, count in sorted(by_source.items()):
        print(f"  - {src:<20}: {count}")
    print(f"  Output saved to      : {output_path}")
    print(f"{'=' * 50}\n")
