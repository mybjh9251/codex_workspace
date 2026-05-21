from __future__ import annotations

from pathlib import Path

from openpyxl import load_workbook

from .models import SatelliteRequest


REQUIRED_HEADERS = ("SAT_Name", "NORAD ID")


def read_satellite_requests(path: Path, sheet_name: str = "Sheet1") -> list[SatelliteRequest]:
    if not path.exists():
        raise FileNotFoundError(f"Missing input file: {path}")

    workbook = load_workbook(path, read_only=True, data_only=True)
    try:
        if sheet_name not in workbook.sheetnames:
            raise ValueError(f"Missing sheet '{sheet_name}' in {path.name}.")

        sheet = workbook[sheet_name]
        header_row = next(sheet.iter_rows(min_row=1, max_row=1, values_only=True), None)
        if not header_row:
            raise ValueError(f"{path.name} has no header row.")

        header_map = _build_header_map(header_row)
        missing = [header for header in REQUIRED_HEADERS if header not in header_map]
        if missing:
            raise ValueError(f"{path.name} is missing required headers: {', '.join(missing)}")

        requests: list[SatelliteRequest] = []
        for row_index, row in enumerate(sheet.iter_rows(min_row=2, values_only=True), start=2):
            sat_name = _cell_text(row, header_map["SAT_Name"])
            norad_id = _cell_text(row, header_map["NORAD ID"])
            if not sat_name and not norad_id:
                continue
            if not sat_name or not norad_id:
                continue
            requests.append(SatelliteRequest(sat_name=sat_name, norad_id=norad_id))

        return requests
    finally:
        workbook.close()


def _build_header_map(header_row: tuple[object, ...]) -> dict[str, int]:
    result: dict[str, int] = {}
    for index, value in enumerate(header_row):
        if value is None:
            continue
        result[str(value).strip()] = index
    return result


def _cell_text(row: tuple[object, ...], index: int) -> str:
    if index >= len(row):
        return ""
    value = row[index]
    if value is None:
        return ""
    if isinstance(value, float) and value.is_integer():
        return str(int(value))
    return str(value).strip()
