"""Scraper for neostar.com/hr/automobili/"""
from __future__ import annotations

import re
from urllib.parse import urljoin

from bs4 import BeautifulSoup, Tag

from config import NEOSTAR_CONFIG
from scrapers.base import (
    CarListing,
    extract_embedded_json,
    extract_from_jsonld,
    extract_fuel,
    extract_mileage,
    extract_power,
    extract_price,
    extract_transmission,
    extract_year,
    jsonld_to_listing,
)

BASE_URL = NEOSTAR_CONFIG["base_url"]


def parse_neostar(html: str, page_url: str) -> list[CarListing]:
    soup = BeautifulSoup(html, "lxml")

    # 1. Try JSON-LD structured data
    jsonld_items = extract_from_jsonld(soup)
    if jsonld_items:
        return [jsonld_to_listing(item, page_url) for item in jsonld_items]

    # 2. Try finding car listing containers
    listings: list[CarListing] = []
    containers = _find_listing_containers(soup)

    if containers:
        for container in containers:
            listing = _parse_container(container, page_url)
            if listing.make or listing.model or listing.price_eur:
                listings.append(listing)
    else:
        # 3. Fallback: parse full page text heuristically
        listings = _heuristic_parse(soup, page_url)

    return listings


def _find_listing_containers(soup: BeautifulSoup) -> list[Tag]:
    for selector in NEOSTAR_CONFIG["listing_selectors"]:
        found = soup.select(selector)
        if len(found) > 1:
            return found
    return []


def _parse_container(tag: Tag, page_url: str) -> CarListing:
    text = tag.get_text(" ", strip=True)
    listing = CarListing(source_url=page_url, source_type="neostar")

    # Title: usually make + model in heading
    for sel in NEOSTAR_CONFIG["title_selectors"]:
        title_tag = tag.select_one(sel)
        if title_tag:
            title = title_tag.get_text(strip=True)
            parts = title.split(None, 1)
            if len(parts) >= 2:
                listing.make = parts[0]
                listing.model = parts[1]
            elif parts:
                listing.model = parts[0]
            break

    listing.price_eur = extract_price(text)
    listing.year = extract_year(text)
    listing.mileage_km = extract_mileage(text)
    listing.fuel_type = extract_fuel(text)
    listing.transmission = extract_transmission(text)
    listing.power_kw = extract_power(text)

    # Listing URL
    link = tag.find("a", href=True)
    if link:
        listing.listing_url = urljoin(page_url, link["href"])

    return listing


def _heuristic_parse(soup: BeautifulSoup, page_url: str) -> list[CarListing]:
    """Last resort: find price patterns in page and build sparse records."""
    listings = []
    full_text = soup.get_text("\n", strip=True)
    lines = [l.strip() for l in full_text.splitlines() if l.strip()]

    # Group lines around price occurrences
    price_line_indices = [i for i, l in enumerate(lines) if re.search(r"\d{3,}.*(?:€|EUR|kn)", l)]
    seen = set()
    for idx in price_line_indices:
        chunk = " ".join(lines[max(0, idx - 5): idx + 3])
        price = extract_price(chunk)
        if price and price not in seen:
            seen.add(price)
            listing = CarListing(source_url=page_url, source_type="neostar")
            listing.price_eur = price
            listing.year = extract_year(chunk)
            listing.mileage_km = extract_mileage(chunk)
            listing.fuel_type = extract_fuel(chunk)
            listing.power_kw = extract_power(chunk)
            listings.append(listing)
    return listings


def get_pagination_urls(html: str, page_url: str) -> list[str]:
    soup = BeautifulSoup(html, "lxml")
    urls = []
    for a in soup.select("a[href]"):
        href = a["href"]
        text = a.get_text(strip=True)
        if re.match(r"^\d+$", text) or any(w in text.lower() for w in ["sljedeć", "next", "»", "›"]):
            full = urljoin(page_url, href)
            if full not in urls and full != page_url:
                urls.append(full)
    return urls
