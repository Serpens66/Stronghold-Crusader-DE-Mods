from pathlib import Path
root=Path(__file__).resolve().parents[2]
mod=root/'Testmods/SpectatorEditorBuildTest'
en={
'Title':'AI test tools','Player':'Player','Actor':'AI receiving the action','Target':'Attack / protection target',
'Placement':'Place units / buildings','StopPlacement':'Stop placing','Goods':'Goods / gold','Orders':'AI orders',
'Ready':'Select an AI and a test tool.','Queued':'Action queued for validation.','Failed':'Action failed. See the timestamped game log.',
'InvalidAmount':'Enter a positive whole number up to 2147483647.','InvalidTarget':'The selected player is no longer available.',
'Overflow':'This amount would exceed the safe balance. Choose a smaller amount.',
'GoldCredited':'Credited {Amount} gold to {Player}.','GoodsCredited':'Added {Amount} goods to the delivery reserve of {Player}.',
'OrderAccepted':'The order is active. Troop movement still depends on units, paths and the running AI.',
'OrderRejected':'The AI declined the order. You can enable Force acceptance for this test.',
'NativeUnavailable':'AI orders are unavailable: native validation failed. Goods and placement remain independent.',
'AiNotRunning':'No initialized AI with a Lord exists in this slot. Orders need a running AI.',
'EnemyRequired':'Select an enemy of the ordered AI as attack target.','NoAi':'No available AI',
'SelectTarget':'Select a target below.','SelectHelp':'Left click selects this player. On the upper AI selector: right click jumps to the Keep, middle click to the Lord.',
'DragHelp':'Drag this title bar to move the tools. The panel stays inside the screen.',
'CollapseHelp':'Collapse or expand the test tools.','GoodsHelp':'Create goods or gold for the selected AI without spending your own resources.',
'OrdersHelp':'Give the selected AI an attack, protection or cancellation order without an alliance requirement. Select the protected Lord for Defend. Diplomacy is unchanged.',
'PlacementTitle':'Place for the selected AI','PlacementHelp':'Enable placement, select an AI, then use the existing building and unit tabs. Right click an AI to jump to its Keep; middle click jumps to its Lord.',
'EditorNotice':'In the map editor, slots may exist before an AI is initialized. Delivery reserves need storage and simulation; orders require a running AI.',
'AmountHelp':'Positive whole amount to add. Existing gold or delivery reserves must leave enough room within the signed 32-bit limit.',
'ReserveHelp':'Goods enter the Vanilla delivery reserve and are stored as space becomes available. Gold is credited immediately. Your own stock is untouched.',
'Credit':'Credit selected amount','CreditHelp':'Add this amount once to the selected AI. It is validated again when executed.',
'Force':'Force acceptance','ForceHelp':'Override only the readiness check for this test order. Does not create troops, alter alliances or guarantee a reachable path.',
'Attack':'Attack target','AttackHelp':'Ask the selected AI to attack the selected enemy.','Defend':'Protect target','DefendHelp':'Ask the selected AI to protect the selected Lord, including itself.',
'Cancel':'Cancel AI order','CancelHelp':'Clear the selected AI’s current requested attack or protection order.',
'Previous':'‹','PreviousHelp':'Show the other four player slots.','Next':'›','NextHelp':'Show the other four player slots.'}
de={
'Title':'KI-Testwerkzeuge','Player':'Spieler','Actor':'Beauftragte KI','Target':'Angriffs- / Schutzziel',
'Placement':'Einheiten / Gebäude platzieren','StopPlacement':'Platzieren beenden','Goods':'Güter / Gold','Orders':'KI-Befehle',
'Ready':'KI und Testwerkzeug auswählen.','Queued':'Aktion zur Prüfung eingereiht.','Failed':'Aktion fehlgeschlagen. Details stehen mit Zeitstempel im Spiellog.',
'InvalidAmount':'Eine positive ganze Zahl bis 2147483647 eingeben.','InvalidTarget':'Der ausgewählte Spieler ist nicht mehr verfügbar.',
'Overflow':'Diese Menge würde den sicheren Wertebereich überschreiten. Eine kleinere Menge wählen.',
'GoldCredited':'{Amount} Gold für {Player} gutgeschrieben.','GoodsCredited':'{Amount} Güter zur Lieferreserve von {Player} hinzugefügt.',
'OrderAccepted':'Der Auftrag ist aktiv. Truppenbewegung hängt weiterhin von Einheiten, Wegen und laufender KI ab.',
'OrderRejected':'Die KI hat den Auftrag abgelehnt. Für diesen Test kann „Annahme erzwingen“ aktiviert werden.',
'NativeUnavailable':'KI-Befehle sind wegen fehlgeschlagener nativer Prüfung gesperrt. Güter und Platzierung bleiben unabhängig nutzbar.',
'AiNotRunning':'In diesem Platz existiert keine initialisierte KI mit Lord. Befehle benötigen eine laufende KI.',
'EnemyRequired':'Als Angriffsziel einen Gegner der beauftragten KI auswählen.','NoAi':'Keine verfügbare KI','SelectTarget':'Unten ein Ziel auswählen.',
'SelectHelp':'Linksklick wählt den Spieler. In der oberen KI-Auswahl springt Rechtsklick zum Bergfried, Mittelklick zum Lord.',
'DragHelp':'An dieser Titelleiste verschieben. Das Fenster bleibt innerhalb des Bildschirms.',
'CollapseHelp':'Testwerkzeuge ein- oder ausklappen.','GoodsHelp':'Güter oder Gold für die ausgewählte KI erzeugen, ohne eigene Vorräte auszugeben.',
'OrdersHelp':'Der ausgewählten KI ohne Bündnispflicht Angreifen, Verteidigen oder Abbrechen vorgeben. Für Verteidigen den zu schützenden Lord auswählen. Die Diplomatie bleibt unverändert.',
'PlacementTitle':'Für die ausgewählte KI platzieren','PlacementHelp':'Platzieren aktivieren, eine KI auswählen und die vorhandenen Gebäude- und Einheitentabs verwenden. Rechtsklick auf eine KI springt zum Bergfried, Mittelklick zum Lord.',
'EditorNotice':'Im Karteneditor können Plätze vor der KI-Initialisierung existieren. Lieferreserven benötigen Lager und Simulation; Befehle eine laufende KI.',
'AmountHelp':'Positive ganze Menge zum Gutschreiben. Goldbestand bzw. Lieferreserve müssen genug Platz im vorzeichenbehafteten 32-Bit-Wertebereich lassen.',
'ReserveHelp':'Güter gelangen in die Vanilla-Lieferreserve und werden bei freiem Lagerplatz eingelagert. Gold wird sofort gutgeschrieben. Eigene Vorräte bleiben unberührt.',
'Credit':'Ausgewählte Menge gutschreiben','CreditHelp':'Diese Menge einmal der ausgewählten KI gutschreiben. Bei Ausführung wird erneut geprüft.',
'Force':'Annahme erzwingen','ForceHelp':'Nur die Bereitschaftsprüfung dieses Testauftrags übergehen. Erzeugt keine Truppen, ändert keine Bündnisse und garantiert keinen erreichbaren Weg.',
'Attack':'Ziel angreifen','AttackHelp':'Die ausgewählte KI zum Angriff auf den ausgewählten Gegner auffordern.',
'Defend':'Ziel schützen','DefendHelp':'Die ausgewählte KI zum Schutz des ausgewählten Lords auffordern, auch ihrer selbst.',
'Cancel':'KI-Auftrag abbrechen','CancelHelp':'Den aktuellen angefragten Angriffs- oder Schutzauftrag der ausgewählten KI löschen.',
'PreviousHelp':'Die anderen vier Spielerplätze anzeigen.','NextHelp':'Die anderen vier Spielerplätze anzeigen.'}
goods={2:('Wood','Holz'),3:('Hops','Hopfen'),4:('Stone','Stein'),6:('Iron','Eisen'),7:('Pitch','Pech'),9:('Wheat','Weizen'),10:('Bread','Brot'),11:('Cheese','Käse'),12:('Meat','Fleisch'),13:('Apples','Äpfel'),14:('Ale','Bier'),15:('Gold','Gold'),16:('Flour','Mehl'),17:('Bows','Bögen'),18:('Crossbows','Armbrüste'),19:('Spears','Speere'),20:('Pikes','Piken'),21:('Maces','Streitkolben'),22:('Swords','Schwerter'),23:('Leather armour','Lederrüstungen'),24:('Metal armour','Metallrüstungen')}
for n,(english,german) in goods.items(): en[f'Good{n:02}']=english; de[f'Good{n:02}']=german
for n in (5,10,25,100,500):
    for sign,label in [('+','Plus'),('−','Minus')]:
        en[label+str(n)]=sign+str(n);de[label+str(n)]=sign+str(n)
        en[label+str(n)+'Help']=f'Adjust the entered amount by {sign}{n}.'
        de[label+str(n)+'Help']=f'Eingegebene Menge um {sign}{n} ändern.'
folder=mod/'Locales';folder.mkdir(exist_ok=True)
for reference in (root/'BugfixesAndQoL/Locales').glob('*.txt'):
    values=dict(en)
    if reference.stem=='de-DE':values.update(de)
    (folder/reference.name).write_text(''.join(f'Tools.{k}={v}\n' for k,v in sorted(values.items())),encoding='utf-8',newline='\r\n')
