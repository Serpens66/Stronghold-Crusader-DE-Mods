-- lua doku: https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/lua-reference.md?ref_type=heads
-- enums: https://rawra-stronghold-crusader.gitlab.io/shcde-script-extender/api/SHCDESE.Interop.eTroops.html?q=etroop
-- event typen (was eventArgs enthält): https://rawra-stronghold-crusader.gitlab.io/shcde-script-extender/api/SHCDESE.EventAPI.Buildings.BuildStructureEventArgs.html

-- TODO:
-- 1) MultiplyGoodsGainInMoneyFrom feuert auch beim Kauf über Markt -> kaufen generiert Geld (OnPlayerMarketInteraction)
-- 2) nicht die Starttruppen für AI multiplizieren, sondern in AIV gucken was als Defense eingetragen ist, und imselben Verhältnis diese truppen spawnen,
  -- weil gespawnde und starttruppen zu defense gehören und wenn schwein 100 leute in defense hat, aber über 100 maceman, dann stellt es keine range in defense mehr rein -> spawned truppen müssen die defense verteilung haben und am besten insg. nicht mehr als gesamt defense
-- brauch ich OnUnloadMap ?
-- 3) mehr priester für church/cathedral?
-- verschieben von gebäuden bei shift+delete?
-- blaupausen planung?
-- auch mit Gebäude am Mauszeiger auf der Minimap bewegen können! und minimap bewegung bei gedrückter maustaste schneller machen...
-- KI kann Soldaten von Spielern überbauen und dadurch löschen: wenn möglich soldaten stattdessen verschieben





-- C# mods:




-- Medium
-- [BuildingCostsRuntime.cs (line 49)](/d:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/BuildingCosts/src/BuildingCostsRuntime.cs:49), [BuildingLimitRuntime.cs (line 64)](/d:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/BuildingLimit/src/BuildingLimitRuntime.cs:64), [StartConditionsRuntime.cs (line 76)](/d:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/StartConditions/src/StartConditionsRuntime.cs:76), [UnitCostsRuntime.cs (line 64)](/d:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/UnitCosts/src/UnitCostsRuntime.cs:64), [UnitLimitRuntime.cs (line 108)](/d:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/UnitLimit/src/UnitLimitRuntime.cs:108): mehrere R3-Subscriptions werden nicht gespeichert und können in Dispose() nicht abgemeldet werden. SomeSettings und die Cache-Klassen machen es korrekt.
-- Fix: pro Runtime List<IDisposable> subscriptions einführen, alle .Subscribe(...)-Rückgaben hinzufügen, in Dispose() disposen/clearen und hooksSubscribed=false setzen.

-- [UnitCostsRuntime.cs (line 410)](/d:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/UnitCosts/src/UnitCostsRuntime.cs:410): Siege-Zusatzkosten blockieren Placement nur für lokalen Spieler. [OnBuildingSpawn (line 453)](/d:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/UnitCosts/src/UnitCostsRuntime.cs:453) überspringt bei fehlenden Ressourcen nur die Zusatzkosten, lässt das Siege Tent aber bestehen. In Multiplayer kann das je nach Autorität/Client-Eventfluss einen Bypass erzeugen.
-- Fix: Host-/Autoritätsmodell klären; notfalls Spawn rückgängig machen oder Kostenprüfung auf der autoritativen Seite erzwingen.

-- [BuildingLimitRuntime.BuildingLimits.cs (line 127)](/d:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/BuildingLimit/src/BuildingLimitRuntime.BuildingLimits.cs:127): Tooltip wird bei jedem HUD_Main.UpdateRollover neu berechnet und Show() aufgerufen. BuildingCosts hat dafür bereits einen Cache; BuildingLimit nicht.
-- Fix: last tooltipStruct/localPlayerId/count/limit cachen und nur bei Änderung ViewModel aktualisieren.

-- Low
-- [UnitCostsRuntime.cs (line 675)](/d:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/UnitCosts/src/UnitCostsRuntime.cs:675): GetMainViewModelMemberValue sucht Field/Property per Reflection bei jedem Hover. Gleiches Muster in UnitLimitRuntime.Tooltips.cs.
-- Fix: FieldInfo/PropertyInfo statisch cachen.

-- [SomeSettingsRuntime.cs (line 107)](/d:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/SomeSettings/src/SomeSettingsRuntime.cs:107): Duplicate-Schutz für Storage-Refund nutzt nur BuildingId. Falls IDs sehr schnell wiederverwendet werden, kann ein echter Refund innerhalb von 2 Sekunden übersprungen werden.
-- Fix: Key um Player/Structure/evtl. Tick erweitern oder Event-spezifisch statt zeitbasiert deduplizieren.














-- OnUnitDeleted wird ausgeführt bei:
-- - Sterben einer unit im Kampf.
-- - NICHT beim auflösen (vermutlich OnUnitTransition? nur in C# nicht lua)
-- - NICHT beim verbrennen einer unit
-- - killingpit?

local MOD_NAME = "SettingsMod"
-- the number means: get "number+1" times the vanilla amount, so 0 means unchanged, 1 means double amount, 2 means tripple
local MultiplyGoodsGainFrom = {AI=0,Human=0}
local MultiplyGoodsGainInMoneyFrom = {AI=2,Human=0} -- eg. number=1 means: everytime the player gets 5 stone, he will also get 35 money (sellprice of 5 stone)
local SetStartGold = {AI=nil,Human=0} -- overwrites the start amount to this amount. set to to nil if you dont want to overwrite the starting money
local AddStartGold = {AI=10000,Human=0} -- add this amount of money to the starting gold
local AdvancedSettings = { -- set to true to enable, false to disable and  nil to not change from ingame setting
  AdvancedOptionsEnabled=true,
  AdvancedSkirmishOptionsEnabled=true,
  BetterHealers=true,
  FasterPeasants=false,
  GlobalImprovedSiegeBehaviour=true,
  GlobalMoreAggressiveSiegeBehaviour=true,
  ImprovedArabSwordsman=true,
  ImprovedFletchers=true,
  ImprovedLadderman=true,
  ImprovedSpearman=true,
  NerfEunuchs=true,
  NoKnockdownWalls=true, -- strong walls
  RebalancedHorseArchers=true,
  UncappedPeasants=false,
  EnemyHealthModifier="VeryStrong", -- Weak, Normal, Strong, VeryStrong. Or nil if you dont want to change it (does not work as of extender version 1.34.0)
}
local overwriteStartGoods = {AI=true,Human=false} -- only if true, SetStartGoods below will replace the start goods
local SetStartGoods = {-- overwrites the start amount to this amount (only all or nothing since Player_SetSkirmishDefaultResources does not work yet properly, so we use ClearIncomingGoods)
  AI = {
    STORED_WOOD_PLANKS = 192,
    STORED_RAW_HOPS = 0,
    STORED_STONE_BLOCKS = 240, -- AI just sells the stone instead of building its castle, so setting that makes AI instantly build the castle is recommended
    STORED_IRON_INGOTS = 48,
    STORED_PITCH_RAW = 48,
    STORED_RAW_WHEAT = 0,
    STORED_FOOD_BREAD = 100,
    STORED_FOOD_CHEESE = 100,
    STORED_FOOD_MEAT = 100,
    STORED_FOOD_FRUIT = 100,
    STORED_FOOD_ALE = 16,
    STORED_FLOUR = 0,
    STORED_BOWS = 0,
    STORED_CROSSBOWS = 0,
    STORED_SPEARS = 0,
    STORED_PIKES = 0,
    STORED_MACES = 0,
    STORED_SWORDS = 0,
    STORED_LEATHER_ARMOUR = 0,
    STORED_METAL_ARMOUR = 0,
  },
  Human = {
    STORED_WOOD_PLANKS = 96, -- default: Skirmish: 100  , Crusader: 100 , Deathmatch: 150
    STORED_RAW_HOPS = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 20
    STORED_STONE_BLOCKS = 48, -- default: Skirmish: 50  , Crusader: 50 , Deathmatch: 150 
    STORED_IRON_INGOTS = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 25
    STORED_PITCH_RAW = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 48
    STORED_RAW_WHEAT = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 25
    STORED_FOOD_BREAD = 50, -- default: Skirmish: 50  , Crusader: 50 , Deathmatch: 200
    STORED_FOOD_CHEESE = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_FOOD_MEAT = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_FOOD_FRUIT = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_FOOD_ALE = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 10
    STORED_FLOUR = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_BOWS = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_CROSSBOWS = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_SPEARS = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_PIKES = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_MACES = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_SWORDS = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_LEATHER_ARMOUR = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
    STORED_METAL_ARMOUR = 0, -- default: Skirmish: 0  , Crusader: 0 , Deathmatch: 0
  },
}
-- spawned extra troops seems not to be used by AI for attack, so most likely just defense, which is fine
local MultiplyStartTroops = {AI=10,Human=0} -- multiplies StartTroops. value of 1 means *2. executed before AddStartTroops to not multiply them
local AddStartTroops = {
  AI = {
    CHIMP_TYPE_ARCHER = 0,
    CHIMP_TYPE_SPEARMAN = 0,
    CHIMP_TYPE_MACEMAN = 0,
    CHIMP_TYPE_XBOWMAN = 0,
    CHIMP_TYPE_PIKEMAN = 0,
    CHIMP_TYPE_SWORDSMAN = 0,
    CHIMP_TYPE_KNIGHT = 0,
    CHIMP_TYPE_ENGINEER = 0,
    CHIMP_TYPE_MONK = 0,
    CHIMP_TYPE_LADDERMAN = 0,
    CHIMP_TYPE_TUNNELER = 0,
    CHIMP_TYPE_ARAB_BOW = 0,
    CHIMP_TYPE_ARAB_SLAVE = 0,
    CHIMP_TYPE_ARAB_SLINGER = 0,
    CHIMP_TYPE_ARAB_ASSASIN = 0,
    CHIMP_TYPE_ARAB_HORSEMAN = 0,
    CHIMP_TYPE_ARAB_SWORDSMAN = 0,
    CHIMP_TYPE_ARAB_GRENADIER = 0,
    CHIMP_TYPE_BEDOUIN_CAMEL_LANCER = 0,
    CHIMP_TYPE_BEDOUIN_HEALER = 0,
    CHIMP_TYPE_BEDOUIN_EUNUCH = 0,
    CHIMP_TYPE_BEDOUIN_AMBUSHER = 0,
    CHIMP_TYPE_BEDOUIN_SKIRMISHER = 0,
    CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL = 0,
    CHIMP_TYPE_BEDOUIN_SAPPER = 0,
    CHIMP_TYPE_BEDOUIN_DEMOLISHER = 0,
  },
  Human = {
    CHIMP_TYPE_ARCHER = 0,
    CHIMP_TYPE_SPEARMAN = 0,
    CHIMP_TYPE_MACEMAN = 0,
    CHIMP_TYPE_XBOWMAN = 0,
    CHIMP_TYPE_PIKEMAN = 0,
    CHIMP_TYPE_SWORDSMAN = 0,
    CHIMP_TYPE_KNIGHT = 0,
    CHIMP_TYPE_ENGINEER = 0,
    CHIMP_TYPE_MONK = 0,
    CHIMP_TYPE_LADDERMAN = 0,
    CHIMP_TYPE_TUNNELER = 0,
    CHIMP_TYPE_ARAB_BOW = 0,
    CHIMP_TYPE_ARAB_SLAVE = 0,
    CHIMP_TYPE_ARAB_SLINGER = 0,
    CHIMP_TYPE_ARAB_ASSASIN = 0,
    CHIMP_TYPE_ARAB_HORSEMAN = 0,
    CHIMP_TYPE_ARAB_SWORDSMAN = 0,
    CHIMP_TYPE_ARAB_GRENADIER = 0,
    CHIMP_TYPE_BEDOUIN_CAMEL_LANCER = 0,
    CHIMP_TYPE_BEDOUIN_HEALER = 0,
    CHIMP_TYPE_BEDOUIN_EUNUCH = 0,
    CHIMP_TYPE_BEDOUIN_AMBUSHER = 0,
    CHIMP_TYPE_BEDOUIN_SKIRMISHER = 0,
    CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL = 0,
    CHIMP_TYPE_BEDOUIN_SAPPER = 0,
    CHIMP_TYPE_BEDOUIN_DEMOLISHER = 0,
  },
}



-- ############################

-- helper functions
local EVENT_ARG_KEYS={"Phase","SkipOriginalFunction","PlayerId","ContextPlayerId","SourcePlayerId","TargetPlayerId","PlayerIdOwner","UnitId","UnitType","TroopType","Good","Amount","BuildingType","StructureType","X","Y","TargetValue1","TargetValue2","ReturnValue","Mappers"}local function vstr(v)if v==nil then return"nil"end;if type(v)=="string"then return string.format("%q",v)end;return tostring(v)end;function event_args_to_string(args)if args==nil then return"eventArgs=nil"end;local parts={}for _,key in ipairs(EVENT_ARG_KEYS)do local ok,value=pcall(function()return args[key]end)if ok and value~=nil then parts[#parts+1]=key.."="..vstr(value)end end;if#parts==0 then return"eventArgs="..tostring(args)end;return"eventArgs{"..table.concat(parts,", ").."}"end;local function log_info(message)local text=MOD_NAME..": "..tostring(message)if Log~=nil and eLogEventLevel~=nil then Log(eLogEventLevel.Information,text)else print(text)end end;local raw_log_info=log_info;local function log_info(...)local parts={}for i=1,select("#",...)do parts[#parts+1]=tostring(select(i,...))end;raw_log_info(table.concat(parts," "))end

local function vec_xy(v)
    local okX, x = pcall(function() return v.X end)
    local okY, y = pcall(function() return v.Y end)
    if not okX or x == nil then
        okX, x = pcall(function() return v.x end)
    end
    if not okY or y == nil then
        okY, y = pcall(function() return v.y end)
    end
    return tonumber(x), tonumber(y)
end
-- ############################


local function HasKeep(playerId)
  local keepId = Player_GetKeepId(playerId)
  return keepId ~= nil and keepId ~= -1 and keepId ~= 0 -- to not execute if eg. in a campaign without keep
end

local function get_xy_near_keep(playerId)
  if not Player_IsPlayerIdValid(playerId) then
    log_info("get_xy_near_keep player not valid")
    return nil,nil,nil
  end
  if Player_GetKeepId(playerId) == -1 then
    log_info("get_xy_near_keep keep not valid")
    return nil,nil,nil
  end
  local door = Player_GetKeepDoorPosition(playerId)
  local doorX, doorY = vec_xy(door)
  if doorX == nil or doorY == nil then
    log_info("get_xy_near_keep door not valid")
    return nil,nil,nil
  end
  local pos = Tile_GetNearestUnoccupiedTile(doorX, doorY, 12)
  local x, y = vec_xy(pos)
  if x == nil or y == nil then
    log_info("get_xy_near_keep NearestUnoccupiedTile not valid")
    return nil,nil,nil
  end
  local tileId = Tile_GetId(x, y)
  if not Tile_IsValid(tileId) or not Tile_IsWalkableAndUnoccupied(tileId) then
    log_info("get_xy_near_keep IsWalkable not valid")
    return nil,nil,nil
  end
  local height = Tile_GetHeight(tileId)
  return x,y,height
end
local function spawn_units_near_keep(playerId, unitType, amount)
  local x,y,height = get_xy_near_keep(playerId)
  if x and y and height and unitType then
    log_info("Unit_CreateLocal",amount," ",unitType,"to player",playerId,"at position",x,y,height)
    for i = 1, amount do
      Unit_CreateLocal(playerId, playerId, x, y, height, unitType) -- playerColorId und ownerId meistens beide playerId
    end
  end
end



local function DoAdvancedSettings()
  for setting,enable in pairs(AdvancedSettings) do
    if setting and enable~=nil then
      local fn = _G["Player_Set"..setting]
      if fn~=nil then
        log_info("Set",setting,"to",enable)
        if setting=="EnemyHealthModifier" then
          if EnemyHPModifier and EnemyHPModifier[enable] then -- EnemyHPModifier is currently not defined and the fn also does not accept numbers, so waiting for a fix of script extender
            fn(EnemyHPModifier[enable])
          end
        else
          fn(enable)
        end
      end
    end
  end
end

local function for_each_alive_player(callback)
  local aliveIds = Player_GetAliveIds() -- returns also slots which are not in the game, so filter also for HasKeep
  for i = 0, aliveIds.Length - 1 do
    local playerId = aliveIds[i] + 1 -- Player_GetAliveIds returns 0 to 7, while 1 to 8 are valid
    if Player_IsPlayerIdValid(playerId) and HasKeep(playerId) then
      -- log_info("for_each_alive_player",playerId)
      callback(playerId)
    end
  end
end

local function DoStartGold()
  for_each_alive_player(function(playerId)
    if HasKeep(playerId) then
      if Player_IsAI(playerId) then
        if SetStartGold["AI"]~=nil then
          Player_SetGold(playerId, SetStartGold["AI"])
          log_info("Set Gold of player",playerId,"to",SetStartGold["AI"])
        end
        if AddStartGold["AI"]~=nil then
          Player_AddGold(playerId, AddStartGold["AI"])
          log_info("Add Gold to player",playerId,":",AddStartGold["AI"])
        end
      else
        if SetStartGold["Human"]~=nil then
          Player_SetGold(playerId, SetStartGold["Human"])
          log_info("Set Gold of player",playerId,"to",SetStartGold["Human"])
        end
        if AddStartGold["Human"]~=nil then
          Player_AddGold(playerId, AddStartGold["Human"])
          log_info("Add Gold to player",playerId,":",AddStartGold["Human"])
        end
      end
    end
  end)
end

local allgoods = {
    STORED_WOOD_PLANKS = true,
    STORED_RAW_HOPS = true,
    STORED_STONE_BLOCKS = true,
    STORED_IRON_INGOTS = true,
    STORED_PITCH_RAW = true,
    STORED_RAW_WHEAT = true,
    STORED_FOOD_BREAD = true,
    STORED_FOOD_CHEESE = true,
    STORED_FOOD_MEAT = true,
    STORED_FOOD_FRUIT = true,
    STORED_FOOD_ALE = true,
    STORED_FLOUR = true,
    STORED_BOWS = true,
    STORED_CROSSBOWS = true,
    STORED_SPEARS = true,
    STORED_PIKES = true,
    STORED_MACES = true,
    STORED_SWORDS = true,
    STORED_LEATHER_ARMOUR = true,
    STORED_METAL_ARMOUR = true,
}


-- https://rawra-stronghold-crusader.gitlab.io/shcde-script-extender/api/SHCDESE.EventAPI.Player.PlayerAddResourceEventArgs.html?q=PlayerAddResourceEventArgs
-- not called by the starting goods, but also by buying goods and even by the function Player_AddGood
local lastGoodAddedByCode = {} -- prevent endless loop
Hooks:GetHook("OnPlayerMarketInteraction"):Subscribe(function(eventArgs)
  log_info("OnPlayerMarketInteraction",event_args_to_string(eventArgs)) -- menge steht nicht drin...
end)

Hooks:GetHook("OnPlayerAddResource"):Subscribe(function(eventArgs)
    if eventArgs.Phase == eEventHookPhase.Post then
        local GoodString = tostring(eventArgs.Good)..tostring(eventArgs.Amount)
        if not lastGoodAddedByCode[eventArgs.PlayerId] or not lastGoodAddedByCode[eventArgs.PlayerId][GoodString] then -- do not continue for goods we just added via Player_AddGood 
            local IsAI = Player_IsAI(eventArgs.PlayerId)
            local multiplygoods = (IsAI and MultiplyGoodsGainFrom["AI"]) or (not IsAI and MultiplyGoodsGainFrom["Human"]) or nil
            -- log_info("OnPlayerAddResource",event_args_to_string(eventArgs))
              if multiplygoods and multiplygoods>0 then
                  if lastGoodAddedByCode[eventArgs.PlayerId]==nil then
                    lastGoodAddedByCode[eventArgs.PlayerId] = {}
                  end
                  local NewGoodString = tostring(eventArgs.Good)..tostring(eventArgs.Amount)
                  lastGoodAddedByCode[eventArgs.PlayerId][NewGoodString] = true
                  Player_AddGood(eventArgs.PlayerId, eventArgs.Good, eventArgs.Amount * multiplygoods)
              end
              local multiplygoodsinmoney = (IsAI and MultiplyGoodsGainInMoneyFrom["AI"]) or (not IsAI and MultiplyGoodsGainInMoneyFrom["Human"]) or nil
              if multiplygoodsinmoney and multiplygoodsinmoney>0 then
                  local pricedata = Player_GetTradeBasePrice(eventArgs.Good) -- 
                  local BuyPrice = pricedata.BuyPrice/5 -- is always for 5 units
                  local SellPrice = pricedata.SellPrice/5
                  -- log_info("OnPlayerAddResource",event_args_to_string(eventArgs), "BuyPrice:",BuyPrice,"SellPrice:",SellPrice)
                  local money = eventArgs.Amount * SellPrice * multiplygoodsinmoney
                  Player_AddGold(eventArgs.PlayerId,money) -- directly convert it to money, to not flood the storage
              end
        else
            lastGoodAddedByCode[eventArgs.PlayerId][GoodString] = nil
        end
    end
end)

-- like Player_ClearIncomingGoods, but does not clear incoming money!
local function customClearIncomingGoods(playerId)
  for good,_ in pairs(allgoods) do
    if eGoods[good]~=nil then
      Player_SubtractIncomingGood(playerId,eGoods[good],-10000)
    end
  end
end

local function ReplaceStartGoods()
  for_each_alive_player(function(playerId)
    if Player_IsAI(playerId) then
      if overwriteStartGoods["AI"] then
        customClearIncomingGoods(playerId)
        for good,amount in pairs(SetStartGoods["AI"]) do
          if amount and amount>0 and eGoods and eGoods[good] then
            Player_AddIncomingGood(playerId,eGoods[good],amount)
            log_info("AddIncomingGood",amount," ",good,"to player",playerId)
          end
        end
      end
    else
      if overwriteStartGoods["Human"] then
        customClearIncomingGoods(playerId)
        for good,amount in pairs(SetStartGoods["Human"]) do
          if amount and amount>0 and eGoods and eGoods[good] then
            Player_AddIncomingGood(playerId,eGoods[good],amount)
            log_info("AddIncomingGood",amount," ",good,"to player",playerId)
          end
        end
      end
    end
  end)
end

local troopsechimps = {
  [eChimps.CHIMP_TYPE_ARCHER]=true,
  [eChimps.CHIMP_TYPE_SPEARMAN]=true,
  [eChimps.CHIMP_TYPE_MACEMAN]=true,
  [eChimps.CHIMP_TYPE_XBOWMAN]=true,
  [eChimps.CHIMP_TYPE_PIKEMAN ]=true,
  [eChimps.CHIMP_TYPE_SWORDSMAN]=true,
  [eChimps.CHIMP_TYPE_KNIGHT]=true,
  [eChimps.CHIMP_TYPE_ENGINEER]=true,
  [eChimps.CHIMP_TYPE_MONK]=true,
  [eChimps.CHIMP_TYPE_LADDERMAN]=true,
  [eChimps.CHIMP_TYPE_TUNNELER]=true,
  [eChimps.CHIMP_TYPE_ARAB_BOW]=true,
  [eChimps.CHIMP_TYPE_ARAB_SLAVE]=true,
  [eChimps.CHIMP_TYPE_ARAB_SLINGER]=true,
  [eChimps.CHIMP_TYPE_ARAB_ASSASIN]=true,
  [eChimps.CHIMP_TYPE_ARAB_HORSEMAN ]=true,
  [eChimps.CHIMP_TYPE_ARAB_SWORDSMAN]=true,
  [eChimps.CHIMP_TYPE_ARAB_GRENADIER]=true,
  [eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER]=true,
  [eChimps.CHIMP_TYPE_BEDOUIN_HEALER]=true,
  [eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH]=true,
  [eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER]=true,
  [eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER]=true,
  [eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL]=true,
  [eChimps.CHIMP_TYPE_BEDOUIN_SAPPER]=true,
  [eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER]=true,
}

local function count_soldiers_for_players()
  local troopcounts = {}
  local units = Unit_GetAllAlive()
  for i = 0, units.Length - 1 do
    local unitId = units[i]
    local playerId = Unit_GetOwner(unitId)
    if troopcounts[playerId]==nil then
      troopcounts[playerId] = {}
    end
    local unitType = Unit_GetType(unitId)
    if troopsechimps[unitType] then
      troopcounts[playerId][unitType] = (troopcounts[playerId][unitType] or 0) + 1
    end
  end
  return troopcounts
end

local function DoAddStartTroops()
  -- log_info("DoAddStartTroops")
  local troopcounts = count_soldiers_for_players()
  for_each_alive_player(function(playerId)
    if Player_IsAI(playerId) then
      if MultiplyStartTroops["AI"] and MultiplyStartTroops["AI"]>0 then
        -- log_info("DoAddStartTroops MultiplyStartTroops",amount)
        for unitType,_ in pairs(troopsechimps) do
          local amount = troopcounts[playerId][unitType]
          -- log_info("DoAddStartTroops troopcounts",playerId,amount)
          if amount and amount>0 then
            amount = amount * MultiplyStartTroops["AI"]
            spawn_units_near_keep(playerId,unitType,amount)
          end
        end
      end
      for unit,amount in pairs(AddStartTroops["AI"]) do
        if amount and amount>0 and eChimps and eChimps[unit] then
          -- log_info("AddStartTroops",amount," ",unit,"to player",playerId)
          spawn_units_near_keep(playerId,eChimps[unit],amount)
        end
      end
    else
      
      Player_SetAutoTrade(eGoods.STORED_WOOD_PLANKS,true,20,200)
      
      -- Player_GetSkirmishDefaultUnitsAmount currently crashes
      if MultiplyStartTroops["Human"] and MultiplyStartTroops["Human"]>0 then
        for unitType,_ in pairs(troopsechimps) do
          if unitType~=nil then
            log_info("DoAddStartTroops unitType",playerId,unitType)
            -- local amount = Player_GetSkirmishDefaultUnitsAmount(unitType) -- only works for humans ... TODO: as soon as we have a function executed before map start, change SkirmishDefaultUnitsAmount directly for humans
            local amount = troopcounts[playerId][unitType]
            if amount and amount>0 then
              amount = amount * MultiplyStartTroops["Human"]
              spawn_units_near_keep(playerId,unitType,amount)
            end
          end
        end
      end
      for unit,amount in pairs(AddStartTroops["Human"]) do
        if amount and amount>0 and eChimps and eChimps[unit] then
          -- log_info("AddStartTroops",amount," ",unit,"to player",playerId)
          spawn_units_near_keep(playerId,eChimps[unit],amount)
        end
      end
    end
  end)
end



local function CodeOnNewGame()
  
  DoAdvancedSettings()
  ReplaceStartGoods()
  DoStartGold()
  Time_AddOneShot(20000, DoAddStartTroops) -- delayed, because it counts the troops after start
    
end

local function CodeOnLoadGame()

    DoAdvancedSettings()
    
    -- ################################
    -- # testing
    -- Player_SetBuildingAvailable(eMappers.MAPPER_GRANARY,false)
    -- Player_SetBuildingAvailable(eMappers.MAPPER_BARRACKS_STONE,false)
    
    -- Player_SetTradeBasePrice()
    
    -- Player_SetBuildingAvailable(building,false)
    -- Player_SetFoodAllowed(playerId,eGoods,false)
    -- Player_SetProductionGoodAllowed(eGoods,false) -- Sets whether a specific weapon type is allowed to be produced in the current map.
    -- Player_SetTradeGoodAllowed(eGoods,false) -- Sets whether a specific good is allowed to be traded at the market in the current map.
    -- Player_SetUnitRecruitable(unit,false) --  Sets whether a specific unit type is allowed to be recruited in the current map.
    -- Player_SetUnitRecruitable(eTroops.TROOP_ARAB_BALLISTA,false) --  Sets whether a specific unit type is allowed to be recruited in the current map.
    -- Player_SetUnitRecruitable(eTroops.TROOP_MACEMAN,false) --  Sets whether a specific unit type is allowed to be recruited in the current map.
    -- Player_SetUnitRecruitable(eTroops.TROOP_SPEARMAN,false) --  Sets whether a specific unit type is allowed to be recruited in the current map.
    
    -- Player_SetAllBuildingsAvailable(true)
    -- Player_SetAllUnitsAllowed(true)
    -- Player_SetAllTradeGoodsAllowed(true)
    -- Player_SetAllProductionGoodsAllowed(true)
    
    -- Player_SetSkirmishDefaultGold(level,gold) -- Sets the default starting gold for a skirmish game.
    -- Player_SetSkirmishDefaultResources(eGoods,amount) -- Sets the default starting resources for a skirmish game.
    -- Player_SetSkirmishDefaultUnitsAmount(unit,amount) -- Sets the default starting unit amount for a skirmish game. NOTE: Not all unit types are supported and may lead to weird results!
end


-- Fresh map start
function mod_init()
    log_info("mod_init called for fresh map start.")
    if Time_AddOneShot ~= nil then -- Time_AddOneShot is ingame time, so depends on ingame speed
      Time_AddOneShot(0, CodeOnNewGame)
    end
end


function mod_load(isEditor)
    log_info("mod_load called. editor=" .. tostring(isEditor))

    if not isEditor then -- Save game loaded  else  Map opened in Map Editor
        if Time_AddOneShot ~= nil then -- Time_AddOneShot is ingame time, so depends on ingame speed
          Time_AddOneShot(0, CodeOnLoadGame)
        end
    end
end

function mod_unload()
    log_info("mod_unload called.")
end



-- Tribe events scheinen nicht von der skirmish ai genutzt zu werden....
-- function OnTribeIssueOrderMoveHere(eventArgs)
    -- if eventArgs.Phase ~= eEventHookPhase.Pre then
        -- return
    -- end
    -- log_info("OnTribeIssueOrderMoveHere",event_args_to_string(eventArgs))
    -- eventArgs.TileX = 300
    -- eventArgs.TileY = 420
-- end
-- function OnTribeIssueOrderWithTarget(eventArgs)
    -- if eventArgs.Phase ~= eEventHookPhase.Pre then
        -- return
    -- end
    -- log_info("OnTribeIssueOrderWithTarget",event_args_to_string(eventArgs))
    -- eventArgs.SkipOriginalFunction = true
    -- eventArgs.ReturnValue = 0
-- end
-- function OnPlayerAIEvaluateAttackOrder(eventArgs)
    -- if eventArgs.Phase ~= eEventHookPhase.Pre then
        -- return
    -- end
    -- log_info("OnPlayerAIEvaluateAttackOrder",event_args_to_string(eventArgs))
    -- eventArgs.TargetPlayerId = andererSpieler
    -- eventArgs.ReturnValue = true
    -- eventArgs.SkipOriginalFunction = true
-- end


-- #########################