from pathlib import Path

from PyInstaller.utils.hooks import collect_all


project_root = Path(SPECPATH)
unity_data, unity_binaries, unity_hidden = collect_all("UnityPy")
pillow_data, pillow_binaries, pillow_hidden = collect_all("PIL")

analysis = Analysis(
    [str(project_root / "run_atlas_builder.py")],
    pathex=[str(project_root)],
    binaries=unity_binaries + pillow_binaries,
    datas=unity_data + pillow_data + [
        (
            str(project_root / "atlas_builder" / "assets" / "supported_gm_groups.json"),
            "atlas_builder/assets",
        )
    ],
    hiddenimports=unity_hidden + pillow_hidden,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(analysis.pure)

exe = EXE(
    pyz,
    analysis.scripts,
    [],
    exclude_binaries=True,
    name="AtlasBuilder",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,
    disable_windowed_traceback=False,
)

collection = COLLECT(
    exe,
    analysis.binaries,
    analysis.datas,
    strip=False,
    upx=True,
    name="AtlasBuilder",
)
