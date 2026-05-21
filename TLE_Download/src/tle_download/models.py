from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class SatelliteRequest:
    sat_name: str
    norad_id: str


@dataclass(frozen=True)
class TleRecord:
    sat_name: str
    norad_id: str
    line1: str
    line2: str
    source: str


@dataclass(frozen=True)
class TleLookupFailure:
    sat_name: str
    norad_id: str
    reason: str
