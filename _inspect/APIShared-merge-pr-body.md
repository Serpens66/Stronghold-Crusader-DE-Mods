Integrate the completed Workshop recovery and history-retention changes from dev/Serpens66 into develop. The branch first merges current develop, retaining the Script Extender binary allow-list documentation alongside the newer recovery documentation.

Validation: fresh Windows PowerShell source/documentation checks and build.bat /nopause /noinstall passed; Core 219/219, APISharedTests 139/139, preset 22/22. No installation, publication or version change. Windows fixtures passed; host Bash runtime fixtures and gameplay checks remain separate.

Use a merge commit to preserve both persistent branches and all existing commits.