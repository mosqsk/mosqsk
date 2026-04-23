"""Scraper for neostar.com/hr/automobili/"""
from __future__ import annotations

import re
from urllib.parse import urljoin, urlparse, parse_qs, urlencode, urlunparse

from bs4 import BeautifulSoup, Tag

from config import NEOSTAR_CONFIG, NEOSTAR_BODY_TYPES
from scrapers.base import (
    CarListing,
    extract_from_jsonld,
    extract_fuel,
    extract_make_model,
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

    # Detect BodyType from URL so we can stamp it on every listing
    body_type_from_url = _body_type_from_url(page_url)

    # 1. Try JSON-LD structured data
    jsonld_items = extract_from_jsonld(soup)
    if jsonld_items:
        listings = [jsonld_to_listing(item, page_url) for item in jsonld_items]
        for l in listings:
            l.source_type = "neostar"
            if not l.body_type and body_type_from_url:
                l.body_type = body_type_from_url
        return listings

    # 2. Try finding car listing containers
    listings: list[CarListing] = []
    containers = _find_listing_containers(soup)

    if containers:
        for container in containers:
            listing = _parse_container(container, page_url)
            if listing.name or listing.price_eur:
                if not listing.body_type and body_type_from_url:
                    listing.body_type = body_type_from_url
                listings.append(listing)
    else:
        # 3. Fallback: heuristic full-page text parse
        listings = _heuristic_parse(soup, page_url)
        for l in listings:
            if not l.body_type and body_type_from_url:
                l.body_type = body_type_from_url

    return listings


def _body_type_from_url(url: str) -> str:
    qs = parse_qs(urlparse(url).query)
    bt_id = qs.get("BodyType", [None])[0]
    if bt_id:
        return NEOSTAR_BODY_TYPES.get(bt_id, f"Type-{bt_id}")
    return ""


def _find_listing_containers(soup: BeautifulSoup) -> list[Tag]:
    for selector in NEOSTAR_CONFIG["listing_selectors"]:
        found = soup.select(selector)
        if len(found) > 1:
            return found
    return []


def _parse_container(tag: Tag, page_url: str) -> CarListing:
    text = tag.get_text(" ", strip=True)
    listing = CarListing(source_url=page_url, source_type="neostar")

    # Title: usually make + model in a heading tag
    for sel in NEOSTAR_CONFIG["title_selectors"]:
        title_tag = tag.select_one(sel)
        if title_tag:
            title = title_tag.get_text(strip=True)
            name, make, model = extract_make_model(title)
            if name:
                listing.name = name
                listing.make = make
                listing.model = model
            else:
                # Title exists but brand not recognised — store as-is
                listing.name = title
                parts = title.split(None, 1)
                listing.make = parts[0] if parts else ""
                listing.model = parts[1] if len(parts) > 1 else ""
            break

    # If no heading found, try brand scan on full container text
    if not listing.name:
        name, make, model = extract_make_model(text)
        listing.name, listing.make, listing.model = name, make, model

    listing.price_eur = extract_price(text)
    listing.year = extract_year(text)
    listing.mileage_km = extract_mileage(text)
    listing.fuel_type = extract_fuel(text)
    listing.transmission = extract_transmission(text)
    listing.power_kw = extract_power(text)

    link = tag.find("a", href=True)
    if link:
        listing.listing_url = urljoin(page_url, link["href"])

    return listing


def _heuristic_parse(soup: BeautifulSoup, page_url: str) -> list[CarListing]:
    """Last resort: find price patterns in page text and build sparse records."""
    listings = []
    full_text = soup.get_text("\n", strip=True)
    lines = [l.strip() for l in full_text.splitlines() if l.strip()]

    price_line_indices = [
        i for i, l in enumerate(lines)
        if re.search(r"\d{3,}.*(?:€|EUR|kn)", l)
    ]
    seen_prices: set[str] = set()

    for idx in price_line_indices:
        # Take a wider window: 8 lines before (where the car name likely is)
        chunk = " ".join(lines[max(0, idx - 8): idx + 3])
        price = extract_price(chunk)
        if not price or price in seen_prices:
            continue
        seen_prices.add(price)

        listing = CarListing(source_url=page_url, source_type="neostar")
        listing.price_eur = price
        listing.year = extract_year(chunk)
        listing.mileage_km = extract_mileage(chunk)
        listing.fuel_type = extract_fuel(chunk)
        listing.power_kw = extract_power(chunk)
        listing.transmission = extract_transmission(chunk)

        name, make, model = extract_make_model(chunk)
        listing.name, listing.make, listing.model = name, make, model

        listings.append(listing)

    return listings


def get_pagination_urls(html: str, page_url: str) -> list[str]:
    """Build the next page URL using neostar's Page= query parameter."""
    soup = BeautifulSoup(html, "lxml")

    # First: look for explicit next-page links in the DOM
    for a in soup.select("a[href]"):
        text = a.get_text(strip=True)
        if any(w in text.lower() for w in ["sljedeć", "next", "»", "›"]):
            full = urljoin(page_url, a["href"])
            if full != page_url:
                return [full]

    # Second: increment the Page= parameter ourselves
    parsed = urlparse(page_url)
    qs = parse_qs(parsed.query)
    current = int(qs.get("Page", ["1"])[0])
    qs["Page"] = [str(current + 1)]
    new_query = urlencode({k: v[0] for k, v in qs.items()})
    next_url = urlunparse(parsed._replace(query=new_query))

    # Only return the next page if it differs from current
    if next_url != page_url:
        return [next_url]
    return []
