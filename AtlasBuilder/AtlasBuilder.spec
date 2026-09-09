from pathlib import Path

from PyInstaller.utils.hooks import collect_data_files, collect_dynamic_libs


project_root = Path(SPECPATH)
unity_data = collect_data_files("UnityPy", includes=["resources/*"])
unity_binaries = collect_dynamic_libs("UnityPy")

analysis = Analysis(
    [str(project_root / "run_atlas_builder.py")],
    pathex=[str(project_root)],
    binaries=unity_binaries,
    datas=unity_data + [
        (
            str(project_root / "atlas_builder" / "assets" / "supported_gm_groups.json"),
            "atlas_builder/assets",
        )
    ],
    hiddenimports=[],
    hookspath=[str(project_root / "hooks")],
    hooksconfig={},
    runtime_hooks=[],
    # Optional fsspec/export integrations are irrelevant for local SHCDE metadata scans.
    excludes=[
        "cv2",
        "IPython",
        "jinja2",
        "lxml",
        "matplotlib",
        "numexpr",
        "openpyxl",
        "pandas",
        "scipy",
        "sklearn",
        "sympy",
        "tables",
        "tensorflow",
        "torch",
        "transformers",
    ],
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
