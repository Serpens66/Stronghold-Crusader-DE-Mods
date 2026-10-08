# APIShared community repository cleanup

Workspace baseline: ac2a17a54f55077aab07c50fe977d674b86aa531.
Independent repository baseline: 77e0c62b361c017ccd4395d2102bd434c744a4ef.
Reviewed public revision: d5bdb5a3be8b858b967da5bca7cc395c07c28858.

Production src, Properties and info.json are unchanged. Versions stay at 0.4.12.
Historical public analysis and instructions are preserved in History, outside the public repository.
The migration scripts and Roslyn extraction are one-time analysis records, not development requirements.

Validation: 18 game-independent MSTest tests, 36 installed-game runtime contract tests and 8 preset tests passed. Public consumer and third-party example compile without friend access. Full elevated build.bat /nopause /noinstall passed. XML, documentation links, package contents, real installed interop, runtime/lifecycle/hooks and CRLF checks passed.
GitHub CI succeeded: https://github.com/SHCDE-APIShared/APIShared/actions/runs/37853759203.
CI intentionally covers only game-independent tests and source/metadata/XAML checks.

Removed source-text assertions are recorded in removed-source-assertions.json. Behavioral assertions remain; real in-game rendering, startup/lifetime, multiplayer host/client transitions, HUD and native movement/gatehouse behavior still require game acceptance tests.
Workspace consumer harnesses retain their own integration and published-preset regressions without compiling partial classes from the public test suites.

Explicit subtree import and final consumer installation results will be recorded after completion.
