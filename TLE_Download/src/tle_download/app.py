from __future__ import annotations

import logging
from pathlib import Path
import sys

import requests

from .clients import CelesTrakClient, SpaceTrackClient, build_record
from .config import load_config
from .excel_reader import read_satellite_requests
from .logging_utils import configure_logging
from .models import TleLookupFailure, TleRecord
from .writer import timestamped_output_path, write_tle_records


def runtime_root() -> Path:
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parents[2]


def run(root: Path | None = None) -> int:
    project_root = root or runtime_root()
    log_path = configure_logging(project_root)
    logging.info("TLE_Download started. root=%s log=%s", project_root, log_path)

    try:
        config = load_config(project_root / "profile.xml")
        requests_ = read_satellite_requests(project_root / "Sat_List.xlsx")
        if not requests_:
            logging.warning("No satellite requests found.")
            return 1

        records, failures = collect_tles(config, requests_)
        output_path = timestamped_output_path(project_root)
        write_tle_records(output_path, records)

        logging.info("Wrote %s records to %s", len(records), output_path)
        if failures:
            for failure in failures:
                logging.warning(
                    "Lookup failed sat_name=%s norad_id=%s reason=%s",
                    failure.sat_name,
                    failure.norad_id,
                    failure.reason,
                )
            logging.warning("Failed lookups: %s", len(failures))

        print(f"Wrote {len(records)} records to {output_path.name}")
        print(f"Failed lookups: {len(failures)}")
        return 0 if records else 1
    except Exception:
        logging.exception("TLE_Download failed.")
        raise


def collect_tles(config, satellite_requests) -> tuple[list[TleRecord], list[TleLookupFailure]]:
    space_track = SpaceTrackClient(config)
    celestrak = CelesTrakClient(config)

    space_track_records: dict[str, tuple[str, str]] = {}
    try:
        space_track.login()
        space_track_records = space_track.fetch_latest_tles(req.norad_id for req in satellite_requests)
        logging.info("Space-Track returned %s NORAD records.", len(space_track_records))
    except requests.RequestException as exc:
        logging.warning("Space-Track lookup unavailable: %s", exc)

    records: list[TleRecord] = []
    failures: list[TleLookupFailure] = []

    for request in satellite_requests:
        tle = space_track_records.get(request.norad_id)
        if tle:
            records.append(build_record(request, tle[0], tle[1], "Space-Track"))
            continue

        try:
            fallback = celestrak.fetch_by_norad_id(request.norad_id)
            if fallback:
                records.append(build_record(request, fallback[0], fallback[1], "CelesTrak CATNR"))
                continue

            name_match = celestrak.fetch_by_exact_name(request)
            if name_match:
                corrected_norad_id, line1, line2 = name_match
                logging.info(
                    "Recovered by exact name sat_name=%s input_norad_id=%s corrected_norad_id=%s",
                    request.sat_name,
                    request.norad_id,
                    corrected_norad_id,
                )
                records.append(build_record(request, line1, line2, "CelesTrak NAME"))
                continue
        except requests.RequestException as exc:
            failures.append(TleLookupFailure(request.sat_name, request.norad_id, str(exc)))
            continue

        failures.append(
            TleLookupFailure(
                request.sat_name,
                request.norad_id,
                "No TLE found from Space-Track or CelesTrak fallback.",
            )
        )

    return records, failures
