# Lobby chat layout [Scope: Vanilla + mod integration]

Observed 2026-10-26 against installed native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
and Assembly-CSharp SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Confirmed-static managed contract; no native RVA or native patch is involved.

## Closed presentation path

`Platform_Multiplayer.HandleIncomingMessage` decodes the Steam lobby payload as
UTF-8. `ChatHandle` obtains the display name and the matching member's colour ID
(or -1), then invokes `LobbyChatDelegate`. Coop and Skirmish register the same
`FRONT_Multiplayer.receivedLobbyChat` consumer. The common `SendChat` command reads
the five possible input controls and sends through `SendLobbyChatMessage`.

`receivedLobbyChat` honors ordinary chat mute, permits classified system hints,
limits individual text to 300 characters, removes LF, appends to `lobbyChat`,
removes the oldest item when its count exceeds 30, sets the hidden Coop-chat
notification, and calls `refreshLobbyChat`. `addSystemLobbyChat` appends colour -1
entries with its separate 100-entry trimming threshold and uses the same renderer.
ShowLobbyScreen and LeaveLobby clear both history and all five display controls.

`refreshLobbyChat(bool)` retains its pending/time gate. `Update` calls its false
branch to perform a pending redraw. An actual redraw clears all five displays,
chooses the active Coop Trail 1-4 or ordinary multiplayer display, rebuilds the
history, scrolls all five viewers to the bottom, and advances its timestamp by
500 ms. Early returns do not rebuild. The pending gate's exact comparison direction
is preserved rather than interpreted as a generic debounce implementation.

Nonnegative colour IDs render this inline sequence:

1. 14x14 shield Image inside InlineUIContainer;
2. name TextBlock (`" " + name + " :"`, width 600, height 14, font 14) inside InlineUIContainer;
3. LineBreak;
4. message TextBlock (font 12, WrapWithOverflow, margin 40/0/5/0) inside InlineUIContainer;
5. Run(Environment.NewLine).

Negative colour IDs use a width-600, height-16, font-14 text container containing
`message + " " + name`, followed by LineBreak. That is a distinct system/unknown-
member presentation branch, not an ordinary named chat row.

All five XAML displays already use a wrapping TextBlock. Their two-line named-row
layout is generated in managed code and cannot be removed by a XAML-only edit.
No gameplay authority is derived from this layout; a compact preference is local.

## Safe compact presentation boundary [mod integration]

BugfixesAndQoL transforms the built inline sequence before the first final
ScrollToBottom call. The foreach enumerator exits with leave to that boundary;
the added callback must inherit that incoming target and remain outside the
enumerator finally handler. A naive insertion before the old target can be skipped
or accidentally extend the handler. The production IL transform is tested against
the installed assembly, including the leave target and handler-end adjustment.

APIShared's LobbyChatPresentation classifies system hints at receivedLobbyChat;
it does not own refreshLobbyChat and remains upstream of this layout transform.
The reviewed local Extender and Fixes sources do not replace refreshLobbyChat.

Offline tests cover literal Unicode/long/empty text, fonts, shield/system retention,
five display slots, repeated enable/disable, reset/rebuild, foreign inline sequences,
rollback after an external collection-write failure, and actual Vanilla IL.
The Noesis boundary doubles do not prove native layout, wrapping, painting or
scroll position. Actual game and two-peer visual acceptance remain separate.
