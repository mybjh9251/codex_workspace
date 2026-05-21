from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import xml.etree.ElementTree as ET


@dataclass(frozen=True)
class AppConfig:
    space_track_id: str
    space_track_pw: str
    timeout_seconds: int
    user_agent: str


def load_config(path: Path) -> AppConfig:
    if not path.exists():
        raise FileNotFoundError(
            f"Missing {path.name}. Copy profile.template.xml to profile.xml and fill credentials."
        )

    root = ET.parse(path).getroot()

    space_track = root.find("space_track")
    request = root.find("request")

    space_track_id = _read_text(space_track, "ID")
    space_track_pw = _read_text(space_track, "PW")
    timeout_text = _read_text(request, "timeout_seconds", default="30")
    user_agent = _read_text(
        request,
        "user_agent",
        default="TLE_Download/1.0",
    )

    try:
        timeout_seconds = int(timeout_text)
    except ValueError as exc:
        raise ValueError("request.timeout_seconds must be an integer.") from exc

    if not space_track_id or not space_track_pw:
        raise ValueError("profile.xml must contain non-empty space_track ID and PW values.")

    return AppConfig(
        space_track_id=space_track_id,
        space_track_pw=space_track_pw,
        timeout_seconds=timeout_seconds,
        user_agent=user_agent,
    )


def _read_text(parent: ET.Element | None, tag: str, default: str = "") -> str:
    if parent is None:
        return default
    child = parent.find(tag)
    if child is None or child.text is None:
        return default
    return child.text.strip()
