from __future__ import annotations

import json
import logging
from typing import Optional

import requests

from .models import SpaceTrackCredentials, TleLines

SPACE_TRACK_LOGIN_URL = "https://www.space-track.org/ajaxauth/login"
SPACE_TRACK_GP_QUERY_TEMPLATE = (
    "https://www.space-track.org/basicspacedata/query/class/gp/"
    "norad_cat_id/{norad_ids}/format/json"
)
CELESTRAK_GP_URL = "https://celestrak.org/NORAD/elements/gp.php"


class SpaceTrackClient:
    def __init__(
        self,
        credentials: SpaceTrackCredentials,
        timeout_seconds: int,
        user_agent: str,
        logger: logging.Logger,
    ) -> None:
        self._credentials = credentials
        self._timeout_seconds = timeout_seconds
        self._logger = logger
        self._session = requests.Session()
        self._session.headers.update({"User-Agent": user_agent})
        self._logged_in = False

    def fetch_many(self, norad_ids: list[str]) -> dict[str, TleLines]:
        if not norad_ids:
            return {}

        self._login_if_needed()
        query_url = SPACE_TRACK_GP_QUERY_TEMPLATE.format(
            norad_ids=",".join(norad_ids)
        )

        response = self._session.get(query_url, timeout=self._timeout_seconds)
        response.raise_for_status()

        try:
            payload = response.json()
        except json.JSONDecodeError as exc:
            raise RuntimeError("Space-Track returned a non-JSON response.") from exc

        if not isinstance(payload, list):
            raise RuntimeError("Space-Track GP query did not return a JSON list.")

        results: dict[str, TleLines] = {}
        for item in payload:
            norad_id = str(item.get("NORAD_CAT_ID", "")).strip()
            line1 = str(item.get("TLE_LINE1", "")).strip()
            line2 = str(item.get("TLE_LINE2", "")).strip()
            if not norad_id or not line1 or not line2:
                continue

            results[norad_id] = TleLines(
                line1=line1,
                line2=line2,
                source="Space-Track",
            )

        return results

    def _login_if_needed(self) -> None:
        if self._logged_in:
            return

        response = self._session.post(
            SPACE_TRACK_LOGIN_URL,
            data={
                "identity": self._credentials.identity,
                "password": self._credentials.password,
            },
            timeout=self._timeout_seconds,
        )
        if response.status_code != 200:
            detail = _summarize_error_response(response)
            raise RuntimeError(
                f"Space-Track login failed with status {response.status_code}: {detail}"
            )

        self._logged_in = True
        self._logger.info("Space-Track login succeeded.")


class CelesTrakClient:
    def __init__(
        self,
        timeout_seconds: int,
        user_agent: str,
    ) -> None:
        self._timeout_seconds = timeout_seconds
        self._session = requests.Session()
        self._session.headers.update({"User-Agent": user_agent})

    def fetch_one(self, norad_id: str) -> Optional[TleLines]:
        response = self._session.get(
            CELESTRAK_GP_URL,
            params={
                "CATNR": norad_id,
                "FORMAT": "2LE",
            },
            timeout=self._timeout_seconds,
        )
        if response.status_code == 404:
            return None
        response.raise_for_status()

        pair = _extract_tle_pair(response.text)
        if pair is None:
            return None

        line1, line2 = pair
        return TleLines(line1=line1, line2=line2, source="CelesTrak")

    def resolve_norad_by_name(self, sat_name: str) -> Optional[str]:
        response = self._session.get(
            CELESTRAK_GP_URL,
            params={
                "NAME": sat_name,
                "FORMAT": "JSON",
            },
            timeout=self._timeout_seconds,
        )
        if response.status_code == 404:
            return None
        response.raise_for_status()

        payload = _parse_celestrak_json_payload(response)
        if payload is None:
            return None

        requested_name = _normalize_name(sat_name)
        exact_matches = []
        for item in payload:
            object_name = _normalize_name(item.get("OBJECT_NAME", ""))
            norad_id = str(item.get("NORAD_CAT_ID", "")).strip()
            if object_name != requested_name or not norad_id:
                continue
            exact_matches.append(norad_id)

        unique_matches = sorted(set(exact_matches))
        if len(unique_matches) != 1:
            return None

        return unique_matches[0]


def _extract_tle_pair(text: str) -> Optional[tuple[str, str]]:
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    for index in range(len(lines) - 1):
        line1 = lines[index]
        line2 = lines[index + 1]
        if line1.startswith("1 ") and line2.startswith("2 "):
            return line1, line2
    return None


def _summarize_error_response(response: requests.Response) -> str:
    content_type = response.headers.get("Content-Type", "")
    if "application/json" in content_type:
        try:
            return json.dumps(response.json(), ensure_ascii=False)
        except json.JSONDecodeError:
            pass
    return response.text.strip()[:300]


def _parse_celestrak_json_payload(
    response: requests.Response,
) -> Optional[list[dict[str, object]]]:
    text = response.text.strip()
    if not text or text == "No GP data found":
        return None

    try:
        payload = response.json()
    except json.JSONDecodeError as exc:
        raise RuntimeError("CelesTrak returned a non-JSON response.") from exc

    if not isinstance(payload, list):
        raise RuntimeError("CelesTrak NAME query did not return a JSON list.")
    return payload


def _normalize_name(value: object) -> str:
    return str(value).strip().casefold()
