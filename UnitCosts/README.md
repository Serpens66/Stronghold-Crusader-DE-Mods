# UnitCosts

- Modify the native gold cost of every recruitable unit (human and AI players)
- Add extra gold and any material costs for **human** players
- Use **No Weapons** for each European unit to remove its four native material requirements for human and AI players; for knights this also removes the horse requirement. Turning it off restores the original requirements.

## Compatibility and diagnostics

This mod requires the Script Extender and **APIShared 0.13.0 or newer**. Subscribe to APIShared separately when using the Steam modpack. Mod settings indicate when gameplay changes are inactive for a direct mission or Trail launch. Use **Customize** where available to start with those changes; edits made while inactive are saved for later games.

Recruitment checks and tooltips use coordinated APIShared hooks, allowing cost and limit policies to share the same interception points.

Routine activity is logged at Debug level. Repeated diagnostics are limited to reduce log spam; warnings and errors remain available. No gameplay setting is needed for this change.
