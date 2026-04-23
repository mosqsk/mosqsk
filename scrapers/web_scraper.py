"""Playwright-based web scraper. Dispatches to site-specific parsers."""
from __future__ import annotations

import sys
import time
from urllib.parse import urlparse

from scrapers.base import CarListing
from scrapers.neostar import parse_neostar, get_pagination_urls as neostar_pages
from scrapers.autozubak import parse_autozubak, get_pagination_urls as autozubak_pages

MAX_PAGES = 10  # safety cap


def _detect_site(url: str) -> str:
    host = urlparse(url).netloc.lower()
    if "neostar" in host:
        return "neostar"
    if "autozubak" in host:
        return "autozubak"
    return "generic"


def scrape_url(url: str, headless: bool = True, max_pages: int = MAX_PAGES) -> list[CarListing]:
    try:
        from playwright.sync_api import sync_playwright, TimeoutError as PWTimeout
    except ImportError:
        print(
            "ERROR: playwright not installed.\n"
            "Run: pip install playwright && playwright install chromium",
            file=sys.stderr,
        )
        return []

    site = _detect_site(url)
    parse_fn = {"neostar": parse_neostar, "autozubak": parse_autozubak}.get(site, _generic_parse)
    pages_fn = {"neostar": neostar_pages, "autozubak": autozubak_pages}.get(site)

    all_listings: list[CarListing] = []
    visited: set[str] = set()
    queue = [url]

    with sync_playwright() as pw:
        browser = pw.chromium.launch(headless=headless)
        context = browser.new_context(
            user_agent=(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                "AppleWebKit/537.36 (KHTML, like Gecko) "
                "Chrome/120.0.0.0 Safari/537.36"
            ),
            locale="hr-HR",
            extra_http_headers={"Accept-Language": "hr,en-US;q=0.9,en;q=0.8"},
        )
        page = context.new_page()

        pages_scraped = 0
        while queue and pages_scraped < max_pages:
            current_url = queue.pop(0)
            if current_url in visited:
                continue
            visited.add(current_url)

            print(f"  Fetching: {current_url}")
            try:
                page.goto(current_url, wait_until="domcontentloaded", timeout=30_000)
                # Wait for listing content to appear
                page.wait_for_timeout(2000)
                # Extra wait for lazy-loaded content
                page.evaluate("window.scrollTo(0, document.body.scrollHeight)")
                page.wait_for_timeout(1500)
                html = page.content()
            except PWTimeout:
                print(f"  WARNING: Timeout fetching {current_url}", file=sys.stderr)
                continue
            except Exception as exc:
                print(f"  WARNING: Failed to fetch {current_url}: {exc}", file=sys.stderr)
                continue

            listings = parse_fn(html, current_url)
            print(f"  Found {len(listings)} listings on this page.")
            all_listings.extend(listings)
            pages_scraped += 1

            # Follow pagination only if a pages function is available
            if pages_fn and pages_scraped < max_pages:
                next_urls = pages_fn(html, current_url)
                for nurl in next_urls:
                    if nurl not in visited and nurl not in queue:
                        queue.append(nurl)

            time.sleep(1)  # polite delay

        browser.close()

    return all_listings


def _generic_parse(html: str, page_url: str) -> list[CarListing]:
    """Heuristic parser for unknown sites."""
    from scrapers.neostar import _heuristic_parse
    from bs4 import BeautifulSoup

    soup = BeautifulSoup(html, "lxml")

    from scrapers.base import extract_from_jsonld, jsonld_to_listing
    jsonld_items = extract_from_jsonld(soup)
    if jsonld_items:
        return [jsonld_to_listing(item, page_url) for item in jsonld_items]

    listings = _heuristic_parse(soup, page_url)
    for l in listings:
        l.source_type = "web-generic"
    return listings
