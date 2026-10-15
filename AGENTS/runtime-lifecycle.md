# Runtime-Mods: Lifecycle, Threads, JSON, Assemblyzugriffe

**Lesen, wenn:** ein C#-/BepInEx-Runtime-Mod (auch Testmod) neu angelegt, geändert oder gebaut wird, insbesondere bei Initialisierung, Events, Hooks, Timern, UI-Updates aus Fremdthreads oder JSON. Gilt zusätzlich zur Root-`AGENTS.md`.

## Hard-Gates vor der ersten Änderung

1. **Lifecycle-/JSON-Inventur:** Projekte und Quellen ausdrücklich auf JSON-Abhängigkeiten und alle Unity-Lifecyclemethoden (`OnDestroy`, `OnDisable`, `OnApplicationQuit` und vergleichbare Teardownpfade) durchsuchen, gegen diese Datei prüfen und das Ergebnis als Zwischenmeldung festhalten — vor der Implementierung, nicht erst nach einem fehlgeschlagenen Build oder Test.
2. **Aufrufweg nachweisen:** Für jede Runtime, die nach `Awake()` weiterarbeitet, den tatsächlichen Aufrufweg und dessen Lebensdauer nach dem SHCDE-Startup-Cleanup nachweisen und nennen. Gilt auch für Testmods und bei jeder späteren Änderung des Auslösers erneut.
3. **Publicized Assembly:** Wird `Assembly-CSharp-publicized.dll` referenziert, alle neu verwendeten Spiel-Felder, -Properties und -Methoden vor Implementierung und vor jedem Build gegen die installierte echte `Assembly-CSharp.dll` auf Sichtbarkeit und Signatur prüfen (maschinell oder mit vollständiger Mitgliederliste dokumentiert). Die publicized Assembly ändert nur die Kompilieransicht; ein erfolgreicher Build beweist keinen gültigen Direktzugriff. Private Mitglieder nur über einen geprüften öffentlichen Vanilla-/Extender-Weg oder einen gekapselten, gegen die echte Assembly validierten Laufzeitzugriff nutzen. `FieldAccessException`/`MethodAccessException` nicht erst im Spiel entdecken.
4. **Events statt Polling:** Immer prüfen, ob es ein passendes Event gibt (z. B. `OnKeyDown` für Tasten statt `onBeforeRender`-Polling), auch vor eigenem Frame-Polling.

## SHCDE-Startup-Cleanup (bestätigt)

- Früh in `BaseUnityPlugin.Awake()` erzeugte Unity-Komponenten — auch `DontDestroyOnLoad`-Objekte und das BepInEx-Manager-GameObject — werden direkt nach dem Chainloader-Start zerstört (Dispatcher-Lifecycle-Report https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/128 und lokale Logs). `OnDisable()`/`OnDestroy()` (inkl. `BaseUnityPlugin.OnDestroy()`) laufen daher schon beim Spielstart und bedeuten weder Prozessende noch Mod-Unload.
- Belegt: Der `RandomEvents`-Host (`DontDestroyOnLoad`) erhielt keine `LateUpdate()`-Callbacks; `UnityMainThreadDispatcher.Instance.StartCoroutine(...)` startete laut Log, führte bei RandomEvents und SpawnCastle aber keinen Callback aus.

## Verwurzelung

- Prozessweit benötigte Runtimeobjekte, Logger, native Hooks, Delegates, R3-Subscriptions, Script-Extender-Events und `GameTimeManagerAPI.OnTick`-Handler statisch oder durch nachweislich langlebige Publisher verwurzeln. Statische Felder allein halten einen Callback auf einer zerstörten Komponente nicht am Leben.
- Solche Objekte nie im Plugin-`OnDestroy()`/`OnDisable()` entfernen, abmelden oder disposen. `Dispose()` darf ausschließlich einen noch nicht veröffentlichten, fehlgeschlagenen Initialisierungskandidaten zurückrollen; kein Plugin-, Startup-, Kartenwechsel- oder Komponentenpfad darf es erreichen.
- Langfristige Arbeit nie über `Update`, `LateUpdate`, `FixedUpdate`, `StartCoroutine` oder andere MonoBehaviour-Callbacks einer früh erzeugten Plugin-/Dispatcher-Komponente.
- Vorlage: `ExtraFeaturesPlugin` (Runtime zuerst durch `CrusaderLibrary.LibraryLoaded` bzw. `CrusaderLibrary.Instance.LibraryLoaded`, dann durch langfristige Publisher-/Event-Subscriptions verwurzelt; kein Startup-Teardown). Unfertige Testmods nicht als Lifecycle-Vorlage verwenden.
- Echte Bereinigung vor Prozessende nur über einen nachweislich echten Shutdownpfad wie `OnApplicationQuit()`, idempotent und nicht durch das Startup-`OnDestroy()` auslösbar. Sonst Ressourcen dem Betriebssystem überlassen.
- Persistente Takte: simulationsgebunden → `GameTimeManagerAPI.OnTick` bzw. passende Extender-Events; render-/kamera-/eingabegebunden (auch bei pausierter Simulation) → statischer `Application.onBeforeRender`-Hook (in SpawnCastle nachgewiesen), per `Time.frameCount` auf einmal pro Frame begrenzt. Eignung jeweils prüfen.

## UnityMainThreadDispatcher

- Nur für den Übergang von Fremdthreads auf den Main-Thread über `Dispatch`, `Enqueue`, `EnqueueDeferred`, `EnqueueStatic`. Nicht als Coroutine-, `Update()`- oder Frame-Host; geerbte MonoBehaviour-Methoden sind keine Dispatcher-API. `EnqueueAndWait` nur nach ausdrücklicher Prüfung.
- Die automatische Neuerzeugung von `UnityMainThreadDispatcher.Instance` ist dispatcher-spezifisch; andere Runtimes, Hooks oder Subscriptions werden nach einem Teardown nicht automatisch wiederhergestellt.

## Threads und UI

- `GameTimeManagerAPI.OnTick`, native Simulationshooks und andere Fremdthread-Callbacks dürfen Noesis-Elemente, gebundene ViewModels und `PropertyChanged` nicht direkt verändern. Synchrone Events laufen auf dem Thread ihres Publishers; das gilt für alle Abonnenten und nachgelagerten Ketten.
- Simulationsentscheidungen im Simulationspfad lassen; nur einen unveränderlichen, bei häufigen Änderungen zusammengefassten UI-Zustand nicht blockierend auf den validierten Main-Thread übertragen, dort anwenden und nur bei echter Änderung benachrichtigen.

## MainViewModel und Editor

- `MainViewModel.Instance` ist kein nebenwirkungsfreier Getter; er kann das ViewModel konstruieren und im frühen Pluginstart in Vanilla-Code scheitern. Editor-Erkennung nur über `Shared.GameModeHelper.IsMapEditor()`; andere Zugriffe vorab mit `MainViewModel.viewModelLoaded` absichern.
- Vanilla setzt `viewModelLoaded` vor der Zuweisung von `HUD_Main`: rendergebundene HUD-Arbeit wartet zusätzlich auf `HUDmain != null`, liest den Singleton erst nach der `viewModelLoaded`-Prüfung und verwirft beim Warten keine ausstehende Arbeit.

## JSON

- Liest oder schreibt ein Runtime-Mod JSON, vom ersten Schritt an ausschließlich `Shared/Runtime/Persistence/DependencyFreeJson.cs` (`Shared.DependencyFreeJson`) per Quelllink verwenden: `<Compile Include="..\Shared\Runtime\Persistence\DependencyFreeJson.cs"><Link>Shared\Runtime\Persistence\DependencyFreeJson.cs</Link></Compile>` (Relativpfad anpassen). Den Parser nicht pro Mod kopieren.
- Verboten im Runtime-Projekt (auch nicht als Prototyp oder Fallback): `System.Web.Extensions`/`JavaScriptSerializer`, `System.Text.Json`, `Newtonsoft.Json`, `DataContractJsonSerializer`, Unitys `JsonUtility` und jede andere Serializer-Assembly.
- APIShared nutzt seinen unabhängig gepflegten internen Parser `APIShared.Internal.DependencyFreeJson` (`APIShared/src/ModSettings/Internal`); beide Implementierungen werden getrennt getestet und nicht automatisch synchronisiert.
- Fachliche Schemata in mod-eigenen Adaptern auf die DOM-Struktur (`Dictionary<string, object>`, `List<object>`, Primitive) abbilden. `Shared.DependencyFreeJson.Serialize(...)` ist für DOM-Werte und einfache Objekte mit öffentlichen lesbaren Properties erlaubt. Schema-, Pflichtfeld-, Bereichs- und Typvalidierung fail-closed im Adapter.
- Eigenständige CLI-/Analyse-/Testprogramme außerhalb des Unity-Prozesses dürfen die JSON-Bibliothek ihres Frameworks nutzen — nicht aber Code, der auch in einem Runtime-Mod kompiliert wird.

## Pflichtprüfungen vor jedem Runtime-Build

- Maschinell prüfen: verbotene JSON-Muster und -Referenzen; Lifecyclemuster (kein prozessweites `Dispose()`, `-=` oder Hook-Uninstall in Plugin-`OnDestroy()`); Plugin-Klassen ohne eigene `Update`/`LateUpdate`/`FixedUpdate`, `StartCoroutine` oder vergleichbare langfristige MonoBehaviour-Wege. Vorher kontrollieren, ob die tatsächlich ausgeführten Prüfscripte diese Fälle abdecken; sonst erweitern oder separat prüfen. Treffer anhand des konkreten Aufrufwegs klären; ein bestandener Teardown- oder JSON-Test ersetzt das nicht.
- Für jede Runtime dokumentieren, wodurch sie nach Zerstörung der Plugin-Komponente verwurzelt bleibt.
- Neuer Runtime-Mod: Logmarker nach dem Startup-Cleanup vorsehen. Der Laufzeitnachweis endet nicht bei `READY`: mindestens ein späterer mod-eigener Karten-, Tick- oder Render-Marker; bei kartenbezogenen Mods `OnStartMap`/`OnLoadSave` bzw. den Missionsstart (siehe [mission-lifecycle.md](mission-lifecycle.md)) und mindestens einen danach ausgeführten Runtime-Tick bestätigen.
