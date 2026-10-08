# Trail Maker custom AIC export and confirmation overlay

Verified 2026-10-08. Native baseline FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Installed Assembly-CSharp SHA-256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789 matches managed/BC8B6A39/decompiled.
The contracts below are managed; no native RVA or new detour is involved.

- FRONT_Multiplayer.MPAIVInfo.Init sets lordName to the supplied name. A standard Lord uses an empty name.
- FRONT_Multiplayer_AISettings.Init obtains standard Lord configs/AIVs through getLordLordList(lordType) and getLordAIVList(lordType) when lordName is empty. LordUser sets builtInLord=false without assigning lordName; config selection assigns lordConfig.
- HUD_IngameMenu.RestartSkirmishMapInfo.importAIVs preserves this state. encode/decode preserves the empty name and custom config; CustomLordConfig.encode/decode does not serialize the source path. An empty name with builtInLord=false and a valid standard Lord type is legitimate.
- CustomisationFileManager.BuildExtendedLordDirectory assigns types by directory name, creates built-in AIV checksum entries and loads .lordjson configs. ConfigSettings.extendedLordPaths maps zero-based type indexes to directory names. Named CustomLords use their own name, including the Workshop prefix; their identity must not be inferred from the standard Lord type.
- FRONT_ManageTrail DoExport uses ShowConfirmationMessage(..., MPConf:true). FRONT_Multiplayer.xaml binds the visible popup overlay to Show_HUD_ConfirmationMP.
- HUD_ConfirmationPopup.ConfirmationClicked clears the visibility flags before invoking the confirmation action. The action exports, rescans the folder and closes Show_TM_Export; it does not subsequently clear the confirmation flags.
- ShowConfirmationOKMessage configures every popup instance but enables only Show_HUD_Confirmation. For Trail Maker export errors, use the already established BugfixesAndQoL ShowMultiplayerConfirmationOkMessage pattern: configure the OK message, disable the ordinary overlay, enable Show_HUD_ConfirmationMP, update popup scale.

Runtime evidence: Extended Data 1.0.8, local LogOutput.log, 2026-10-08 21:37:30.261: SelectIndex -> Capture -> CreateDefinition -> Prepare -> ExportHook fails with an empty author Lord name. The user confirmed a standard Lord with custom .lordjson and no visible popup.
This demonstrates the pre-fix failures. Post-fix export, launch and popup visibility still require a gameplay acceptance run.
