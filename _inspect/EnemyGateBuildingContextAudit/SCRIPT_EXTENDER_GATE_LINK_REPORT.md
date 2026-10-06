# GameBuilding.r_GatehouseId semantic clarification

For Native SHA256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2,
connection update dispatch F4540 uses class table3125D0. Gate handlers D7E90
(class3) and D8040 (class4) write the current connection record index to
building-manager + buildingId*0x32C + 0x32E. This maps to GameBuilding offset
0x2D2, currently named r_GatehouseId. It is not a validated drawbridge-parent
Building-ID. Spatial coupling instead uses C5300/B9330 perimeter lookup.

Please clarify the field documentation (at least for gatehouses) to prevent
consumers treating this value as a linked building ID. No Script Extender source
change was made locally. Both handlers sample componentC at gate origin+(1,1);
this physical endpoint is reconstructible from verified oriented A/B coordinates.
