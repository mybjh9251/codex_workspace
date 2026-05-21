from __future__ import annotations

from collections.abc import Iterable
import re

import requests

from .config import AppConfig
from .models import SatelliteRequest, TleRecord


TLE_LINE1_RE = re.compile(r"^1\s+(\d{1,6})\S*")
TLE_LINE2_RE = re.compile(r"^2\s+(\d{1,6})\s+")


class SpaceTrackClient:
    base_url = "https://www.space-track.org"

    def __init__(self, config: AppConfig):
        self.config = config
        self.session = requests.Session()
        self.session.headers.update({"User-Agent": config.user_agent})

    def login(self) -> None:
        response = self.session.post(
            f"{self.base_url}/ajaxauth/login",
            data={
                "identity": self.config.space_track_id,
                "password": self.config.space_track_pw,
            },
            timeout=self.config.timeout_seconds,
        )
        response.raise_for_status()

    def fetch_latest_tles(self, norad_ids: Iterable[str]) -> dict[str, tuple[str, str]]:
        ids = sorted({str(norad_id).strip() for norad_id in norad_ids if str(norad_id).strip()})
        if not ids:
            return {}

        id_text = ",".join(ids)
        url = (
            f"{self.base_url}/basicspacedata/query/class/gp/"
            f"NORAD_CAT_ID/{id_text}/orderby/NORAD_CAT_ID,EPOCH desc/format/tle"
        )
        response = self.session.get(url, timeout=self.config.timeout_seconds)
        response.raise_for_status()
        return parse_tle_text_by_norad(response.text)


class CelesTrakClient:
    base_url = "https://celestrak.org/NORAD/elements/gp.php"

    def __init__(self, config: AppConfig):
        self.config = config
        self.session = requests.Session()
        self.session.headers.update({"User-Agent": config.user_agent})

    def fetch_by_norad_id(self, norad_id: str) -> tuple[str, str] | None:
        response = self.session.get(
            self.base_url,
            params={"CATNR": norad_id, "FORMAT": "TLE"},
            timeout=self.config.timeout_seconds,
        )
        response.raise_for_status()
        records = parse_named_tle_text(response.text)
        if not records:
            return None
        return records[0][1], records[0][2]

    def fetch_by_exact_name(self, request: SatelliteRequest) -> tuple[str, str, str] | None:
        response = self.session.get(
            self.base_url,
            params={"NAME": request.sat_name, "FORMAT": "TLE"},
            timeout=self.config.timeout_seconds,
        )
        response.raise_for_status()
        matches = [
            record
            for record in parse_named_tle_text(response.text)
            if record[0].strip().casefold() == request.sat_name.strip().casefold()
        ]
        if len(matches) != 1:
            return None
        name, line1, line2 = matches[0]
        norad_id = norad_from_tle_lines(line1, line2) or request.norad_id
        return norad_id, line1, line2


def parse_tle_text_by_norad(text: str) -> dict[str, tuple[str, str]]:
    result: dict[str, tuple[str, str]] = {}
    for _name, line1, line2 in parse_named_tle_text(text):
        norad_id = norad_from_tle_lines(line1, line2)
        if norad_id and norad_id not in result:
            result[norad_id] = (line1, line2)
    return result


def parse_named_tle_text(text: str) -> list[tuple[str, str, str]]:
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    records: list[tuple[str, str, str]] = []
    i = 0
    while i + 2 < len(lines):
        if lines[i + 1].startswith("1 ") and lines[i + 2].startswith("2 "):
            records.append((lines[i], lines[i + 1], lines[i + 2]))
            i += 3
        elif lines[i].startswith("1 ") and lines[i + 1].startswith("2 "):
            norad_id = norad_from_tle_lines(lines[i], lines[i + 1]) or "UNKNOWN"
            records.append((norad_id, lines[i], lines[i + 1]))
            i += 2
        else:
            i += 1
    return records


def norad_from_tle_lines(line1: str, line2: str) -> str | None:
    match1 = TLE_LINE1_RE.match(line1)
    match2 = TLE_LINE2_RE.match(line2)
    if not match1 or not match2:
        return None
    if match1.group(1) != match2.group(1):
        return None
    return match1.group(1)


def build_record(request: SatelliteRequest, line1: str, line2: str, source: str) -> TleRecord:
    return TleRecord(
        sat_name=request.sat_name,
        norad_id=norad_from_tle_lines(line1, line2) or request.norad_id,
        line1=line1,
        line2=line2,
        source=source,
    )
