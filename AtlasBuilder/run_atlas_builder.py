import sys
from pathlib import Path

from atlas_builder.core import build_project, prepare_project
from atlas_builder.gm_groups import SUPPORTED_GROUPS
from atlas_builder.gui import run_gui
from atlas_builder.models import ProjectConfig


if __name__ == "__main__":
    # These switches are release checks; normal users always enter through the GUI.
    if "--self-test" in sys.argv:
        if len(SUPPORTED_GROUPS) != 195 or "tile_ruins" not in SUPPORTED_GROUPS:
            raise SystemExit(1)
        raise SystemExit(0)
    if "--validate-project" in sys.argv:
        index = sys.argv.index("--validate-project")
        project = ProjectConfig.load(Path(sys.argv[index + 1]))
        prepare_project(project)
        raise SystemExit(0)
    if "--build-project" in sys.argv:
        index = sys.argv.index("--build-project")
        project = ProjectConfig.load(Path(sys.argv[index + 1]))
        project.output_mod_directory = sys.argv[index + 2]
        build_project(project, overwrite_existing=True)
        raise SystemExit(0)
    run_gui()
