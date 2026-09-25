# Thasos-Oracle mit installierter Vanilla-Karte

Die archivierten Korpora verweisen auf die heute fehlende Kopie `v_Thasos.map`.
Die installierte `StreamingAssets/Maps/Thasos.map` besitzt exakt den in beiden
Korpora erwarteten SHA-256
`84DCF2A480A4334DFC0C4BAE54DA49BACFE1D7B31F1D9AD2E171CF1F3B60275C`.
Nur `Map.Path` wurde in zwei abgeleiteten Manifesten auf diese Datei gesetzt;
die Originalmanifeste und alle Native-Fälle blieben unverändert.

| Korpus | Original-SHA-256 | Fälle | Exakt | Bewusst grau | Abweichung | Fehler |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Session-aware | `544D04EE63AB50DF9E643F79E62888E47FF56FECBF0FAAE9A0CE9BA854FBC46A` | 24 | 4 | 20 | 0 | 0 |
| Session-aware paired | `2B25A936CEBCEDD6300AD8676D82AE407A45549FFC7DDA03E6E38993987264CD` | 48 | 8 | 40 | 0 | 0 |

Der Vergleich prüft den unveränderten Offline-Vanilla-Fit. Die neue
Praxisfarbe ist eine getrennte geometrische Bewertung und fließt nicht in
den Oracle-Score ein. Native-Basis:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
