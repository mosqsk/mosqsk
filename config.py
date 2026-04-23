import re

BROWSER_HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/120.0.0.0 Safari/537.36"
    ),
    "Accept-Language": "hr,en-US;q=0.9,en;q=0.8",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
}

# Croatian number format: 15.990 → 15990, 15.990,50 → 15990.50
def parse_croatian_number(text: str) -> float | None:
    if not text:
        return None
    cleaned = text.strip().replace(" ", "")
    # Remove thousands dots, replace decimal comma
    cleaned = re.sub(r"\.(?=\d{3})", "", cleaned)
    cleaned = cleaned.replace(",", ".")
    match = re.search(r"[\d]+(?:\.\d+)?", cleaned)
    if match:
        try:
            return float(match.group())
        except ValueError:
            return None
    return None


PRICE_RE = re.compile(
    r"([\d]{1,3}(?:[.\s]\d{3})*(?:,\d{1,2})?)\s*(?:€|EUR|eur|kn|HRK)",
    re.IGNORECASE,
)
YEAR_RE = re.compile(r"\b(19[5-9]\d|20[0-2]\d)\b")
MILEAGE_RE = re.compile(r"([\d]{1,3}(?:[.\s]\d{3})*)\s*km", re.IGNORECASE)
KW_RE = re.compile(r"(\d{2,4})\s*(?:kW|KW)")
HP_RE = re.compile(r"(\d{2,4})\s*(?:ks|KS|hp|HP|PS)")

FUEL_KEYWORDS = {
    "diesel": ["dizel", "diesel", "tdi", "cdi", "dci", "cdti", "d4d", "crdi"],
    "petrol": ["benzin", "petrol", "benzinsk", "tsi", "tfsi", "gsi", "fsi"],
    "electric": ["električni", "electric", "bev", "ev", " e-"],
    "hybrid": ["hibrid", "hybrid", "hev", "phev", "plug-in"],
    "lpg": ["plin", "lpg", "cng"],
}

TRANSMISSION_KEYWORDS = {
    "automatic": ["automatik", "automatic", "automat", "dsg", "cvt", "pdc"],
    "manual": ["manual", "ručni", "rucni", "6-brzinsk", "5-brzinsk"],
}

NEOSTAR_CONFIG = {
    "base_url": "https://www.neostar.com/hr/automobili/",
    "listing_selectors": [
        ".vehicle-item",
        ".car-item",
        ".offer-item",
        ".listing-item",
        "article.car",
        ".vehicle-card",
        ".vozilo",
        "[class*='vehicle']",
        "[class*='car-card']",
        "[class*='offer']",
    ],
    "title_selectors": ["h2", "h3", ".title", ".name", "[class*='title']", "[class*='name']"],
    "price_selectors": [".price", "[class*='price']", "[class*='cijena']"],
    "detail_selectors": [".specs", ".details", "[class*='spec']", "[class*='detail']", "ul", ".info"],
}

AUTOZUBAK_CONFIG = {
    "base_url": "https://www.autozubak.hr/rabljeni-auti/",
    "listing_selectors": [
        ".vehicle-item",
        ".car-item",
        ".offer-item",
        "article",
        ".car-card",
        ".vozilo-item",
        "[class*='vehicle']",
        "[class*='car']",
        "[class*='offer']",
        "[class*='auto']",
    ],
    "title_selectors": ["h2", "h3", "h4", ".title", ".name", "[class*='title']", "[class*='name']"],
    "price_selectors": [".price", "[class*='price']", "[class*='cijena']"],
    "detail_selectors": [".specs", ".details", "[class*='spec']", "[class*='detail']", "ul", ".info"],
}
