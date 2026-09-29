# Minimap notification video contract

Verified against the installed game assemblies during the BugfixesAndQoL 1.0.172 last-frame integration:

- `Assembly-CSharp.dll` SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
- `Noesis.NoesisGUI.dll` SHA-256: `98476D3CA84AE0F2DCFBADDCC64B01A1F65474BD44402673FD6856D1B5347648`.
- `SFXManager.playBink(string, bool, bool)` writes `requestBinkPlaybackURI` before `FatControler` assigns the URI to the minimap `MediaElement`.
- The installed `NoesisMediaPlayer` treats a URI ending in `**` as single playback with `MediaEnded`; a plain URI loops.
- `MainHUD.RadarME_Ended(object, RoutedEventArgs)` keeps state 3 while speech channel 1 is still playing when `binkWaitForSpeech` is true. `FatControler` later calls the parameterless `RadarME_Ended()` to clean up after speech ends.
- Vanilla left-click sets the minimap media opacity to zero. The separate right-click notification completion remains available while the retained video is visible.

The single-playback and final-frame path was confirmed in game with `NotificationLastFrameTest`; the new option in BugfixesAndQoL still requires its own game test.
