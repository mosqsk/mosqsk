#!/usr/bin/env python3
"""
Car Offers Scraper - Croatia
CLI tool to scrape car listings from websites and local files.

Usage examples:
  # Scrape websites
  python main.py --url https://www.neostar.com/hr/automobili/
  python main.py --url https://www.autozubak.hr/rabljeni-auti/

  # Parse local files
  python main.py --file offers.pdf
  python main.py --file offers.xlsx
  python main.py --file prices.csv

  # Parse all files in a folder
  python main.py --folder /path/to/offers/

  # Mix sources and custom output
  python main.py --url https://... --folder ./data/ --output my_results.csv

  # Visible browser (useful for debugging / bot-protected sites)
  python main.py --url https://... --no-headless
"""
from __future__ import annotations

import argparse
import sys

from output.writer import write_csv, print_summary
from scrapers.base import CarListing


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="car-scraper",
        description="Scrape Croatian car offers from websites, PDFs, CSV, and Excel files.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )

    sources = parser.add_argument_group("Input sources (at least one required)")
    sources.add_argument(
        "--url",
        action="append",
        metavar="URL",
        dest="urls",
        default=[],
        help="Website URL to scrape (can be repeated for multiple sites)",
    )
    sources.add_argument(
        "--file",
        action="append",
        metavar="PATH",
        dest="files",
        default=[],
        help="Local file to parse: .pdf, .csv, .xls, .xlsx (can be repeated)",
    )
    sources.add_argument(
        "--folder",
        action="append",
        metavar="PATH",
        dest="folders",
        default=[],
        help="Folder to scan for supported files (recursive, can be repeated)",
    )

    options = parser.add_argument_group("Options")
    options.add_argument(
        "--output",
        metavar="FILE",
        default=None,
        help="Output CSV file path (default: car_offers_TIMESTAMP.csv)",
    )
    options.add_argument(
        "--max-pages",
        type=int,
        default=10,
        metavar="N",
        help="Maximum pages to scrape per website (default: 10)",
    )
    options.add_argument(
        "--no-headless",
        action="store_true",
        help="Show browser window while scraping (useful for debugging)",
    )
    options.add_argument(
        "--no-pagination",
        action="store_true",
        help="Only scrape the given URL, do not follow pagination",
    )

    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    if not args.urls and not args.files and not args.folders:
        parser.print_help()
        print("\nERROR: Provide at least one --url, --file, or --folder.", file=sys.stderr)
        return 1

    all_listings: list[CarListing] = []

    # ── Web scraping ──────────────────────────────────────────────────────────
    if args.urls:
        from scrapers.web_scraper import scrape_url

        max_pages = 1 if args.no_pagination else args.max_pages
        headless = not args.no_headless

        for url in args.urls:
            print(f"\nScraping website: {url}")
            try:
                listings = scrape_url(url, headless=headless, max_pages=max_pages)
                all_listings.extend(listings)
            except Exception as exc:
                print(f"  ERROR scraping {url}: {exc}", file=sys.stderr)

    # ── File parsing ──────────────────────────────────────────────────────────
    if args.files:
        from parsers.file_parser import parse_file

        for file_path in args.files:
            print(f"\nParsing file: {file_path}")
            try:
                listings = parse_file(file_path)
                all_listings.extend(listings)
            except Exception as exc:
                print(f"  ERROR parsing {file_path}: {exc}", file=sys.stderr)

    # ── Folder scanning ───────────────────────────────────────────────────────
    if args.folders:
        from parsers.file_parser import parse_folder

        for folder_path in args.folders:
            print(f"\nScanning folder: {folder_path}")
            try:
                listings = parse_folder(folder_path)
                all_listings.extend(listings)
            except Exception as exc:
                print(f"  ERROR scanning {folder_path}: {exc}", file=sys.stderr)

    # ── Output ────────────────────────────────────────────────────────────────
    if not all_listings:
        print("\nNo listings found. Check your inputs or try --no-headless to debug.")
        return 0

    output_path = write_csv(all_listings, args.output)
    print_summary(all_listings, output_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
