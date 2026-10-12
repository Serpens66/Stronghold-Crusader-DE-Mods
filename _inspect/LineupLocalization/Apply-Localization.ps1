$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$mod = Join-Path $workspace 'BugfixesAndQoL'
$utf8 = [Text.UTF8Encoding]::new($false)
function Write-CheckedText([string]$path, [string]$text) {
    $expected = [regex]::Replace($text, '\r?\n', [Environment]::NewLine)
    [IO.File]::WriteAllText($path, $expected, $utf8)
    if (-not [string]::Equals([IO.File]::ReadAllText($path), $expected, [StringComparison]::Ordinal)) { throw "Write verification failed: $path" }
    if ($expected -match '(?<!\r)\n|\\r\\n') { throw "Invalid line endings: $path" }
    Write-Output ("CRLF {0}: {1}" -f ([regex]::Matches($expected, '\r\n').Count), $path)
}

# Each record: locale, 19 short labels, feature title, feature help, saved-row help.
$records = @'
en-US
Lineup|Density|Tight|Normal|Far|Very far|Ranged / siege|Off|Rear|Center|Role markers|On|Default|Block|Line|Column|Wedge|Circle|Remember rows
Enable unit lineup and complete target markers
Choose the target lineup, density and role placement in the troop HUD. Units do not march in formation. Hold the configured Move mouse button over open ground and drag to set facing. Wheel up removes a row (wider); wheel down adds a row (longer). Circle and Default keep their geometry. Set density in the window only. Release issues one synchronized move. Requires at least two selected units. Shift waypoints retain their existing behavior.
Use the mouse wheel while holding a move command to change width/length. Remembers rows per lineup across restarts. Smaller groups temporarily limit rows. Off or no saved value: automatic rows. Excludes Circle and Default.
de-DE
Aufstellung|Dichte|Eng|Normal|Weit|Sehr weit|Fernkampf / Belagerung|Aus|Hinten|Mitte|Rollenmarker|An|Standard|Block|Linie|Kolonne|Keil|Kreis|Breite/Länge merken
Aufstellung und vollständige Zielmarker aktivieren
Wähle Zielaufstellung, Dichte und Rollenplatzierung im Truppen-HUD. Einheiten marschieren nicht in Formation. Halte die konfigurierte Bewegungs-Maustaste über freiem Boden gedrückt und ziehe für die Blickrichtung. Mausrad hoch entfernt eine Reihe (breiter), runter fügt eine hinzu (länger). Kreis und Standard behalten ihre Geometrie. Dichte nur im Fenster einstellen. Loslassen erteilt einen synchronisierten Bewegungsbefehl. Erfordert mindestens zwei ausgewählte Einheiten. Shift-Wegpunkte behalten ihr bisheriges Verhalten.
Mausrad bei gehaltenem Bewegungsbefehl: Breite/Länge ändern. Merkt die Reihenanzahl je Aufstellungsart, auch nach Neustarts. Kleinere Gruppen begrenzen sie vorübergehend. Aus oder ohne gespeicherten Wert: automatische Reihenanzahl. Gilt nicht für Kreis und Standard.
fr-FR
Disposition|Densité|Serrée|Normale|Espacée|Très espacée|Tir / siège|Non|Arrière|Centre|Repères de rôle|Oui|Par défaut|Bloc|Ligne|Colonne|Coin|Cercle|Mémoriser les rangs
Activer la disposition des unités et tous les repères de destination
Choisissez la disposition à destination, la densité et le placement des rôles dans l'interface des troupes. Les unités ne marchent pas en formation. Maintenez le bouton de déplacement configuré sur un terrain libre et faites glisser pour orienter les unités. Molette vers le haut : un rang en moins (plus large) ; vers le bas : un rang en plus (plus long). Cercle et Par défaut conservent leur géométrie. Réglez la densité uniquement dans la fenêtre. Relâcher envoie un déplacement synchronisé. Au moins deux unités sont nécessaires. Les étapes avec Maj restent inchangées.
Utilisez la molette pendant un ordre de déplacement maintenu pour changer la largeur/longueur. Mémorise les rangs par disposition entre les redémarrages. Les petits groupes limitent temporairement les rangs. Désactivé ou sans valeur mémorisée : rangs automatiques. Exclut Cercle et Par défaut.
es-ES
Disposición|Densidad|Compacta|Normal|Amplia|Muy amplia|Tiro / asedio|No|Detrás|Centro|Marcas de rol|Sí|Por defecto|Bloque|Línea|Columna|Cuña|Círculo|Recordar filas
Activar la disposición de unidades y todos los marcadores de destino
Elige la disposición de destino, la densidad y la ubicación de los roles en la interfaz de tropas. Las unidades no marchan en formación. Mantén el botón de movimiento configurado sobre terreno libre y arrastra para orientar las unidades. Rueda arriba: una fila menos (más ancho); abajo: una fila más (más largo). Círculo y Por defecto conservan su geometría. Ajusta la densidad solo en la ventana. Al soltar se envía un movimiento sincronizado. Requiere al menos dos unidades seleccionadas. Los puntos de ruta con Mayús no cambian.
Usa la rueda mientras mantienes una orden de movimiento para cambiar el ancho/largo. Recuerda las filas de cada disposición entre reinicios. Los grupos pequeños limitan las filas temporalmente. Desactivado o sin valor guardado: filas automáticas. No se aplica a Círculo ni Por defecto.
it-IT
Disposizione|Densità|Stretta|Normale|Ampia|Molto ampia|Tiro / assedio|No|Dietro|Centro|Indicatori ruolo|Sì|Predefinita|Blocco|Linea|Colonna|Cuneo|Cerchio|Ricorda le file
Attiva la disposizione delle unità e tutti gli indicatori di destinazione
Scegli disposizione a destinazione, densità e posizione dei ruoli nell'interfaccia delle truppe. Le unità non marciano in formazione. Tieni premuto il pulsante di movimento configurato sul terreno libero e trascina per orientare le unità. Rotella in su: una fila in meno (più largo); in giù: una fila in più (più lungo). Cerchio e Predefinita mantengono la geometria. Regola la densità solo nella finestra. Rilascia per inviare un movimento sincronizzato. Servono almeno due unità selezionate. I punti di percorso con Maiusc restano invariati.
Usa la rotella durante un ordine di movimento tenuto premuto per cambiare larghezza/lunghezza. Ricorda le file per disposizione anche dopo il riavvio. I gruppi piccoli limitano temporaneamente le file. Disattivato o senza valore salvato: file automatiche. Esclude Cerchio e Predefinita.
pt-BR
Disposição|Densidade|Compacta|Normal|Ampla|Muito ampla|Tiro / cerco|Não|Atrás|Centro|Marcas de função|Sim|Padrão|Bloco|Linha|Coluna|Cunha|Círculo|Lembrar fileiras
Ativar disposição das unidades e todos os marcadores de destino
Escolha a disposição no destino, a densidade e a posição das funções na interface das tropas. As unidades não marcham em formação. Segure o botão de movimento configurado sobre terreno livre e arraste para definir a direção. Roda para cima: uma fileira a menos (mais largo); para baixo: uma a mais (mais longo). Círculo e Padrão mantêm a geometria. Ajuste a densidade apenas na janela. Soltar envia um movimento sincronizado. Requer pelo menos duas unidades selecionadas. Os pontos de rota com Shift não mudam.
Use a roda durante uma ordem de movimento mantida para mudar largura/comprimento. Lembra as fileiras por disposição após reiniciar. Grupos menores limitam as fileiras temporariamente. Desativado ou sem valor salvo: fileiras automáticas. Não se aplica a Círculo nem Padrão.
nl-NL
Opstelling|Dichtheid|Dicht|Normaal|Ruim|Zeer ruim|Afstand / belegering|Uit|Achter|Midden|Rolmarkeringen|Aan|Standaard|Blok|Linie|Kolom|Wig|Cirkel|Rijen onthouden
Eenheidsopstelling en volledige doelmarkeringen inschakelen
Kies de doelopstelling, dichtheid en rolplaatsing in de troepeninterface. Eenheden marcheren niet in formatie. Houd de ingestelde bewegingsknop boven vrij terrein ingedrukt en sleep om de richting te bepalen. Wiel omhoog: een rij minder (breder); omlaag: een rij meer (langer). Cirkel en Standaard behouden hun geometrie. Stel dichtheid alleen in het venster in. Loslaten geeft één gesynchroniseerd bewegingsbevel. Vereist minstens twee geselecteerde eenheden. Routepunten met Shift blijven ongewijzigd.
Gebruik het muiswiel tijdens een ingedrukt bewegingsbevel om breedte/lengte te wijzigen. Onthoudt rijen per opstelling na herstarten. Kleinere groepen beperken rijen tijdelijk. Uit of geen opgeslagen waarde: automatische rijen. Geldt niet voor Cirkel en Standaard.
sv-SE
Uppställning|Täthet|Tät|Normal|Gles|Mycket gles|Skytte / belägring|Av|Bak|Mitten|Rollmarkörer|På|Standard|Block|Linje|Kolonn|Kil|Cirkel|Kom ihåg rader
Aktivera enheternas uppställning och fullständiga målmarkörer
Välj uppställning vid målet, täthet och rollplacering i truppgränssnittet. Enheterna marscherar inte i formation. Håll den inställda förflyttningsknappen över öppen mark och dra för att ange riktning. Mushjulet uppåt tar bort en rad (bredare); nedåt lägger till en rad (längre). Cirkel och Standard behåller sin geometri. Ställ endast in täthet i fönstret. När du släpper ges en synkroniserad förflyttning. Kräver minst två valda enheter. Ruttpunkter med Skift fungerar som tidigare.
Använd mushjulet medan du håller ett förflyttningskommando för att ändra bredd/längd. Minns rader per uppställning efter omstart. Mindre grupper begränsar rader tillfälligt. Av eller inget sparat värde: automatiska rader. Gäller inte Cirkel och Standard.
pl-PL
Ustawienie|Gęstość|Ciasno|Normalnie|Luźno|Bardzo luźno|Strzelcy / oblężenie|Wył.|Tył|Środek|Znaczniki ról|Wł.|Domyślne|Blok|Linia|Kolumna|Klin|Krąg|Zapamiętaj rzędy
Włącz ustawienie jednostek i pełne znaczniki celu
Wybierz ustawienie docelowe, gęstość i rozmieszczenie ról w interfejsie wojsk. Jednostki nie maszerują w szyku. Przytrzymaj skonfigurowany przycisk ruchu nad wolnym terenem i przeciągnij, aby ustawić kierunek. Kółko w górę usuwa rząd (szerzej); w dół dodaje rząd (dłużej). Krąg i Domyślne zachowują geometrię. Gęstość ustawiaj tylko w oknie. Zwolnienie wysyła jeden zsynchronizowany rozkaz ruchu. Wymaga co najmniej dwóch wybranych jednostek. Punkty trasy z Shift działają jak dotąd.
Użyj kółka przy przytrzymanym rozkazie ruchu, aby zmienić szerokość/długość. Zapamiętuje rzędy dla każdego ustawienia po restarcie. Mniejsze grupy tymczasowo ograniczają rzędy. Wyłączone lub brak zapisu: rzędy automatyczne. Nie dotyczy Kręgu i Domyślnego.
cs-CZ
Rozestavení|Hustota|Těsné|Normální|Volné|Velmi volné|Střelci / obléhání|Vyp.|Vzadu|Střed|Značky rolí|Zap.|Výchozí|Blok|Řada|Kolona|Klín|Kruh|Pamatovat řady
Zapnout rozestavení jednotek a všechny cílové značky
Vyberte cílové rozestavení, hustotu a umístění rolí v rozhraní vojsk. Jednotky nepochodují ve formaci. Podržte nastavené tlačítko pohybu nad volným terénem a tažením určete směr. Kolečko nahoru odebere řadu (širší); dolů přidá řadu (delší). Kruh a Výchozí zachovávají geometrii. Hustotu nastavujte pouze v okně. Uvolnění vydá jeden synchronizovaný rozkaz pohybu. Vyžaduje alespoň dvě vybrané jednotky. Body trasy se Shift fungují jako dosud.
Kolečkem při podrženém rozkazu pohybu měňte šířku/délku. Pamatuje řady pro každé rozestavení i po restartu. Menší skupiny řady dočasně omezují. Vypnuto nebo bez uložené hodnoty: automatické řady. Neplatí pro Kruh a Výchozí.
hu-HU
Felállás|Sűrűség|Szoros|Normál|Laza|Nagyon laza|Lövész / ostrom|Ki|Hátul|Középen|Szerepjelölők|Be|Alapértelmezett|Tömb|Sor|Oszlop|Ék|Kör|Sorok megjegyzése
Egységek felállásának és teljes céljelölőinek engedélyezése
Válaszd ki a célbeli felállást, sűrűséget és szerepek elhelyezését a csapatok felületén. Az egységek nem menetelnek alakzatban. Tartsd lenyomva a beállított mozgásgombot szabad terepen, és húzással állítsd be az irányt. Görgő felfelé: egy sorral kevesebb (szélesebb); lefelé: egy sorral több (hosszabb). A Kör és az Alapértelmezett megtartja geometriáját. A sűrűséget csak az ablakban állítsd. Felengedéskor egy szinkronizált mozgásparancs indul. Legalább két kijelölt egység szükséges. A Shift útvonalpontjai változatlanok.
Tartott mozgásparancsnál a görgő módosítja a szélességet/hosszt. Felállásonként megjegyzi a sorokat újraindítás után is. Kisebb csoportok ideiglenesen korlátozzák a sorokat. Kikapcsolva vagy mentés nélkül: automatikus sorok. Körre és Alapértelmezettre nem érvényes.
el-GR
Διάταξη|Πυκνότητα|Πυκνή|Κανονική|Αραιή|Πολύ αραιή|Βολές / πολιορκία|Όχι|Πίσω|Κέντρο|Δείκτες ρόλων|Ναι|Προεπιλογή|Μπλοκ|Γραμμή|Φάλαγγα|Σφήνα|Κύκλος|Αποθήκευση σειρών
Ενεργοποίηση διάταξης μονάδων και όλων των δεικτών προορισμού
Επιλέξτε διάταξη προορισμού, πυκνότητα και θέσεις ρόλων στη διεπαφή στρατευμάτων. Οι μονάδες δεν βαδίζουν σε σχηματισμό. Κρατήστε το ρυθμισμένο κουμπί κίνησης πάνω από ελεύθερο έδαφος και σύρετε για προσανατολισμό. Ροδέλα πάνω: μία σειρά λιγότερη (πλατύτερα)· κάτω: μία περισσότερη (μακρύτερα). Κύκλος και Προεπιλογή διατηρούν τη γεωμετρία τους. Ρυθμίστε πυκνότητα μόνο στο παράθυρο. Η απελευθέρωση δίνει μία συγχρονισμένη κίνηση. Απαιτούνται τουλάχιστον δύο επιλεγμένες μονάδες. Τα σημεία πορείας με Shift παραμένουν ίδια.
Χρησιμοποιήστε τη ροδέλα ενώ κρατάτε εντολή κίνησης για αλλαγή πλάτους/μήκους. Αποθηκεύει σειρές ανά διάταξη και μετά την επανεκκίνηση. Μικρότερες ομάδες περιορίζουν προσωρινά τις σειρές. Ανενεργό ή χωρίς αποθήκευση: αυτόματες σειρές. Εξαιρούνται Κύκλος και Προεπιλογή.
ru-RU
Расстановка|Плотность|Тесно|Обычно|Свободно|Очень свободно|Стрелки / осада|Выкл.|Сзади|Центр|Метки ролей|Вкл.|По умолчанию|Блок|Линия|Колонна|Клин|Круг|Запоминать ряды
Включить расстановку войск и все маркеры цели
Выберите расстановку в точке назначения, плотность и размещение ролей в интерфейсе войск. Юниты не идут строем. Удерживайте настроенную кнопку движения над свободной землёй и тяните для выбора направления. Колесо вверх убирает ряд (шире); вниз добавляет ряд (длиннее). Круг и По умолчанию сохраняют геометрию. Плотность меняется только в окне. Отпускание отдаёт один синхронизированный приказ движения. Нужно минимум два выбранных юнита. Точки маршрута с Shift работают как прежде.
Колесо при удержании приказа движения меняет ширину/длину. Запоминает ряды для каждой расстановки после перезапуска. Малые группы временно ограничивают ряды. Выключено или нет сохранённого значения: автоматические ряды. Не действует для Круга и По умолчанию.
uk-UA
Шикування|Щільність|Тісно|Звичайно|Вільно|Дуже вільно|Стрільці / облога|Вимк.|Позаду|Центр|Позначки ролей|Увімк.|Типове|Блок|Лінія|Колона|Клин|Коло|Запам'ятати ряди
Увімкнути шикування військ і всі позначки цілі
Виберіть шикування в точці призначення, щільність і розміщення ролей в інтерфейсі військ. Юніти не марширують строєм. Утримуйте налаштовану кнопку руху над вільною землею та тягніть для вибору напрямку. Колесо вгору прибирає ряд (ширше); вниз додає ряд (довше). Коло й Типове зберігають геометрію. Щільність змінюється лише у вікні. Відпускання віддає один синхронізований наказ руху. Потрібно щонайменше два вибрані юніти. Точки маршруту з Shift працюють як раніше.
Колесо під час утримання наказу руху змінює ширину/довжину. Запам'ятовує ряди для кожного шикування після перезапуску. Малі групи тимчасово обмежують ряди. Вимкнено або немає збереженого значення: автоматичні ряди. Не діє для Кола й Типового.
tr-TR
Diziliş|Yoğunluk|Sık|Normal|Seyrek|Çok seyrek|Atış / kuşatma|Kapalı|Arka|Merkez|Rol işaretleri|Açık|Varsayılan|Blok|Hat|Kol|Kama|Çember|Sıraları hatırla
Birim dizilişini ve tüm hedef işaretlerini etkinleştir
Birlik arayüzünde hedef dizilişini, yoğunluğu ve rol yerleşimini seçin. Birimler düzenli formasyonda yürümez. Ayarlanan hareket düğmesini boş arazide basılı tutup yön için sürükleyin. Tekerlek yukarı bir sıra çıkarır (daha geniş); aşağı bir sıra ekler (daha uzun). Çember ve Varsayılan geometrisini korur. Yoğunluğu yalnızca pencerede ayarlayın. Bırakmak tek bir eşzamanlı hareket emri verir. En az iki seçili birim gerekir. Shift ile yol noktaları değişmez.
Hareket emrini basılı tutarken tekerlekle genişliği/uzunluğu değiştirin. Her dizilişin sıralarını yeniden başlatınca da hatırlar. Küçük gruplar sıraları geçici olarak sınırlar. Kapalı veya kayıt yoksa: otomatik sıralar. Çember ve Varsayılan için geçerli değildir.
ar
التموضع|الكثافة|متقارب|عادي|متباعد|متباعد جداً|رماة / حصار|إيقاف|الخلف|الوسط|علامات الأدوار|تشغيل|افتراضي|كتلة|صف|عمود|إسفين|دائرة|تذكر الصفوف
تفعيل تموضع الوحدات وجميع علامات الوجهة
اختر التموضع عند الوجهة والكثافة ومواقع الأدوار في واجهة القوات. لا تسير الوحدات في تشكيل. اضغط باستمرار على زر الحركة المحدد فوق أرض خالية واسحب لتحديد الاتجاه. عجلة الفأرة للأعلى تحذف صفاً (أعرض) وللأسفل تضيف صفاً (أطول). تحتفظ الدائرة والافتراضي بهندستهما. اضبط الكثافة في النافذة فقط. الإفلات يصدر أمر حركة متزامناً واحداً. يلزم تحديد وحدتين على الأقل. نقاط المسار باستخدام Shift لا تتغير.
استخدم عجلة الفأرة مع الاستمرار في أمر الحركة لتغيير العرض والطول. يتذكر الصفوف لكل تموضع بعد إعادة التشغيل. المجموعات الصغيرة تحد الصفوف مؤقتاً. عند الإيقاف أو عدم وجود قيمة محفوظة: صفوف تلقائية. لا يشمل الدائرة والافتراضي.
ja-JP
配置|密度|密集|標準|広め|非常に広い|遠隔 / 攻城|オフ|後方|中央|役割マーカー|オン|既定|ブロック|横隊|縦隊|くさび|円|列数を記憶
部隊配置と全目的地マーカーを有効化
部隊HUDで目的地の配置、密度、役割ごとの位置を選びます。ユニットは隊形を維持して行進しません。設定された移動用マウスボタンを空き地で押したままドラッグすると向きを指定できます。ホイール上で列を減らし（幅を広げる）、下で列を増やします（奥行きを伸ばす）。円と既定は形状を維持します。密度はウィンドウ内でのみ設定します。ボタンを離すと同期された移動命令を1回発行します。2体以上の選択が必要です。Shiftの経由地点は従来どおりです。
移動ボタンを押したままホイールで幅と奥行きを変えます。配置ごとの列数を再起動後も記憶します。少人数の部隊では列数を一時的に制限します。オフまたは保存値がない場合は自動設定です。円と既定は対象外です。
ko-KR
배치|밀도|좁게|보통|넓게|매우 넓게|원거리 / 공성|끄기|후방|중앙|역할 표시|켜기|기본|블록|횡대|종대|쐐기|원형|행 수 기억
부대 배치 및 모든 목적지 표시 활성화
부대 HUD에서 목적지 배치, 밀도, 역할별 위치를 선택합니다. 유닛은 대형을 유지하며 행군하지 않습니다. 빈 땅에서 설정된 이동 마우스 버튼을 누른 채 끌어 방향을 정합니다. 휠을 위로 돌리면 행이 줄어 넓어지고, 아래로 돌리면 행이 늘어 길어집니다. 원형과 기본은 모양을 유지합니다. 밀도는 창에서만 설정합니다. 버튼을 놓으면 동기화된 이동 명령이 한 번 내려집니다. 두 유닛 이상을 선택해야 합니다. Shift 경유지는 기존대로 작동합니다.
이동 명령을 누른 상태에서 휠로 폭과 길이를 바꿉니다. 배치별 행 수를 재시작 후에도 기억합니다. 작은 부대는 행 수를 일시적으로 제한합니다. 끄거나 저장값이 없으면 자동 행 수를 사용합니다. 원형과 기본은 제외됩니다.
zh-CN
排列|密度|紧密|普通|宽松|非常宽松|远程 / 攻城|关|后方|中央|角色标记|开|默认|方块|横列|纵列|楔形|圆形|记住行数
启用部队排列和完整目标标记
在部队界面选择目的地的排列、密度和角色位置。单位不会保持队形行进。在空地上按住设置的移动鼠标键并拖动以指定朝向。滚轮向上减少一行（更宽），向下增加一行（更长）。圆形和默认保持原有形状。密度只能在窗口中设置。松开后发出一次同步移动命令。至少需要选择两个单位。Shift路径点保持原有行为。
按住移动命令时用滚轮改变宽度和长度。记住各排列的行数，重启后仍保留。较小部队会暂时限制行数。关闭或没有保存值时自动设置行数。不适用于圆形和默认。
zh-HK
排列|密度|緊密|普通|寬鬆|非常寬鬆|遠程 / 攻城|關|後方|中央|角色標記|開|預設|方塊|橫列|縱列|楔形|圓形|記住行數
啟用部隊排列和完整目標標記
在部隊介面選擇目的地的排列、密度和角色位置。單位不會保持隊形行進。在空地上按住設定的移動滑鼠鍵並拖曳以指定朝向。滾輪向上減少一行（更寬），向下增加一行（更長）。圓形和預設保持原有形狀。密度只能在視窗中設定。放開後發出一次同步移動命令。至少需要選取兩個單位。Shift路徑點保持原有行為。
按住移動命令時用滾輪改變寬度和長度。記住各排列的行數，重新啟動後仍保留。較小部隊會暫時限制行數。關閉或沒有儲存值時自動設定行數。不適用於圓形和預設。
th-TH
การจัดวาง|ความหนาแน่น|ชิด|ปกติ|ห่าง|ห่างมาก|ยิงไกล / ล้อม|ปิด|ด้านหลัง|กึ่งกลาง|ตัวระบุบทบาท|เปิด|ค่าเริ่มต้น|บล็อก|แถว|แนวตอน|ลิ่ม|วงกลม|จำจำนวนแถว
เปิดใช้การจัดวางหน่วยและเครื่องหมายเป้าหมายทั้งหมด
เลือกการจัดวางที่ปลายทาง ความหนาแน่น และตำแหน่งตามบทบาทในหน้าจอกองทัพ หน่วยจะไม่เดินโดยรักษารูปขบวน กดปุ่มเมาส์สำหรับเคลื่อนที่ที่ตั้งไว้ค้างบนพื้นที่ว่างแล้วลากเพื่อกำหนดทิศ หมุนล้อขึ้นเพื่อลดหนึ่งแถว (กว้างขึ้น) หรือลงเพื่อเพิ่มหนึ่งแถว (ยาวขึ้น) วงกลมและค่าเริ่มต้นคงรูปเดิม ตั้งความหนาแน่นในหน้าต่างเท่านั้น ปล่อยปุ่มเพื่อส่งคำสั่งเคลื่อนที่ที่ซิงโครไนซ์หนึ่งครั้ง ต้องเลือกอย่างน้อยสองหน่วย จุดผ่านทางด้วย Shift ทำงานเหมือนเดิม
ใช้ล้อเมาส์ขณะกดคำสั่งเคลื่อนที่ค้างเพื่อเปลี่ยนความกว้างและความยาว จำจำนวนแถวของแต่ละการจัดวางแม้เริ่มใหม่ กลุ่มเล็กจำกัดแถวชั่วคราว หากปิดหรือไม่มีค่าที่บันทึกจะใช้แถวอัตโนมัติ ไม่ใช้กับวงกลมและค่าเริ่มต้น
'@ -split '\r?\n'
$labelKeys = @('ArrangementTooltip','LineupDensity','LineupTight','LineupNormal','LineupFar','LineupVeryFar','LineupRangedSiege','LineupOff','LineupRear','LineupCenter','LineupRoleMarkers','LineupOn','LineupVanilla','LineupBlock','LineupLine','LineupColumn','LineupWedge','LineupCircle','RememberRows')
if ($records.Count -ne 105) { throw "Expected 21 records: $($records.Count) lines" }
for ($index = 0; $index -lt $records.Count; $index += 5) {
    $locale = $records[$index]
    $labels = $records[$index + 1] -split '\|'
    if ($labels.Count -ne $labelKeys.Count) { throw "Label count: $locale" }
    $updates = [ordered]@{}
    for ($labelIndex = 0; $labelIndex -lt $labelKeys.Count; $labelIndex++) { $updates['BugfixesAndQoL.' + $labelKeys[$labelIndex]] = $labels[$labelIndex] }
    $updates['BugfixesAndQoL.EnableMoveFormationEnhancements'] = $records[$index + 2]
    $updates['BugfixesAndQoL.EnableMoveFormationEnhancementsHelp'] = $records[$index + 3]
    $updates['BugfixesAndQoL.RememberRowsHelp'] = $records[$index + 4]
    $path = Join-Path $mod ('Locales\' + $locale + '.txt')
    $text = [IO.File]::ReadAllText($path)
    $additions = [Collections.Generic.List[string]]::new()
    foreach ($key in $updates.Keys) {
        $pattern = '(?m)^' + [regex]::Escape($key) + '=[^\r\n]*'
        $replacement = $key + '=' + $updates[$key]
        if ([regex]::IsMatch($text, $pattern)) {
            $text = [regex]::Replace($text, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($match) $replacement })
        } else { $additions.Add($replacement) }
    }
    if ($additions.Count) { $text = $text.TrimEnd([char]13, [char]10) + [Environment]::NewLine + ($additions -join [Environment]::NewLine) + [Environment]::NewLine }
    Write-CheckedText $path $text
}

$hudPath = Join-Path $mod 'Patches\Assets\GUI\XAMLResources\HUD_Troops.xaml'
$hudText = [IO.File]::ReadAllText($hudPath)
$bindings = [ordered]@{
    'Text="Aufstellung"' = 'Text="{Binding LineupText}" TextWrapping="Wrap"'
    'Text="Density"' = 'Text="{Binding DensityText}" TextWrapping="Wrap"'
    'Text="Ranged / siege"' = 'Text="{Binding RangedSiegeText}" TextWrapping="Wrap"'
    'Text="Role markers"' = 'Text="{Binding RoleMarkersText}" TextWrapping="Wrap" MaxWidth="120"'
    'Content="Tight"' = 'Content="{Binding TightText}" FontSize="12"'
    'Content="Normal"' = 'Content="{Binding NormalText}" FontSize="12"'
    'Content="Far"' = 'Content="{Binding FarText}" FontSize="12"'
    'Content="Very Far"' = 'Content="{Binding VeryFarText}" FontSize="12"'
    'Content="Off"' = 'Content="{Binding OffText}" FontSize="12"'
    'Content="Rear"' = 'Content="{Binding RearText}" FontSize="12"'
    'Content="Center"' = 'Content="{Binding CenterText}" FontSize="12"'
    'Content="On"' = 'Content="{Binding OnText}" FontSize="12"'
}
foreach ($binding in $bindings.Keys) {
    if (-not $hudText.Contains($binding)) { throw "Missing original attribute: $binding" }
    $hudText = $hudText.Replace($binding, $bindings[$binding])
}
Write-CheckedText $hudPath $hudText
foreach ($relative in @('src\FormationMenuViewModel.cs', 'info.json')) {
    $path = Join-Path $mod $relative
    Write-CheckedText $path ([IO.File]::ReadAllText($path))
}
