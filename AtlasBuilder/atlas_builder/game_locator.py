from __future__ import annotations

import os
import re
from pathlib import Path


GAME_DIRECTORY = "Stronghold Crusader Definitive Edition"
DATA_DIRECTORY = GAME_DIRECTORY + "_Data"
APP_MANIFEST = "appmanifest_3024040.acf"


def _steam_roots() -> list[Path]:
    roots: list[Path] = []
    try:
        import winreg

        for hive, key_name in (
            (winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam"),
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam"),
        ):
            try:
                with winreg.OpenKey(hive, key_name) as key:
                    value, _kind = winreg.QueryValueEx(key, "SteamPath" if hive == winreg.HKEY_CURRENT_USER else "InstallPath")
                    roots.append(Path(value))
            except OSError:
                pass
    except ImportError:
        pass
    for environment_name in ("ProgramFiles(x86)", "ProgramFiles"):
        value = os.environ.get(environment_name)
        if value:
            roots.append(Path(value) / "Steam")
    # This is also the maintainer's known local installation and remains a harmless fallback elsewhere.
    roots.append(Path(r"E:\ProgrammeE\Steam"))
    return list(dict.fromkeys(path.resolve() for path in roots))


def _library_roots(steam_root: Path) -> list[Path]:
    libraries = [steam_root]
    file = steam_root / "steamapps" / "libraryfolders.vdf"
    try:
        text = file.read_text(encoding="utf-8-sig", errors="replace")
    except OSError:
        return libraries
    for match in re.finditer(r'"path"\s+"([^"]+)"', text):
        libraries.append(Path(match.group(1).replace(r"\\", "\\")))
    return list(dict.fromkeys(path.resolve() for path in libraries))


def find_game_data_directory() -> str:
    for steam_root in _steam_roots():
        for library in _library_roots(steam_root):
            steamapps = library / "steamapps"
            manifest = steamapps / APP_MANIFEST
            install_dir = GAME_DIRECTORY
            try:
                text = manifest.read_text(encoding="utf-8-sig", errors="replace")
                match = re.search(r'"installdir"\s+"([^"]+)"', text)
                if match:
                    install_dir = match.group(1)
            except OSError:
                pass
            candidate = steamapps / "common" / install_dir / DATA_DIRECTORY
            if (candidate / "resources.assets").is_file():
                return str(candidate)
    return ""
