using LordMessageReplacementTest;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

int checks = 0;
void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); checks++; }
const string audio = "fx\\speech\\all_req_01.wav";
const string video = "bad_soldier_taunt.webm";
foreach (var lord in new[] { AILords.SK_RAT, AILords.SK_SNAKE, AILords.SK_PIG, AILords.SK_WOLF })
    Check(ReplacementPolicy.ShouldReplace(lord, AILordMessageType.RequestGoods, audio, video, audio, video), "Missing target lord " + lord);
Check(!ReplacementPolicy.ShouldReplace(AILords.SK_SALADIN, AILordMessageType.RequestGoods, audio, video, audio, video), "Other lords must remain unchanged.");
Check(ReplacementPolicy.ShouldReplace(AILords.SK_RAT, AILordMessageType.RequestGoods, audio, video, "FX/speech/ALL_REQ_01.wav", "BAD_SOLDIER_TAUNT.WEBM"), "Path spelling must not hide the same source.");
Check(!ReplacementPolicy.ShouldReplace(AILords.SK_RAT, AILordMessageType.RequestGoods, audio, video, "_SLA_/author/fx/speech/all_req_01.wav", video), "Do not overwrite an authored SE voice.");
Check(!ReplacementPolicy.ShouldReplace(AILords.SK_RAT, AILordMessageType.RequestGoods, audio, video, audio, "_SLA_/author/bad_soldier_taunt.webm"), "Do not overwrite an authored SE video.");
Check(!ReplacementPolicy.ShouldReplace(AILords.SK_RAT, AILordMessageType.DieAlly, "fx/speech/rt_anger_02.wav", "rt_anger1.webm", "fx/speech/rt_anger_02.wav", "rt_anger1.webm"), "Preserve Rat's individual defeat.");
Check(!ReplacementPolicy.ShouldReplace(AILords.SK_WOLF, AILordMessageType.KillNpc, "fx/speech/all_vict_04.wav", video, "fx/speech/all_vict_04.wav", video), "Wolf KillNpc is outside the allied filter.");
Check(ReplacementPolicy.ShouldReplace(AILords.SK_SNAKE, AILordMessageType.DieAlly, "fx/speech/all_ally_death_01.wav", "bad_soldier_nevous.webm", "fx/speech/all_ally_death_01.wav", "bad_soldier_nevous.webm"), "Snake's generic defeat is included.");
Check(!ReplacementPolicy.IsAlliedCategory(AILordMessageType.IncomingMessage), "Do not alter the preliminary announcement.");
Check(!ReplacementPolicy.IsAlliedCategory(AILordMessageType.Unk21) && !ReplacementPolicy.IsAlliedCategory(AILordMessageType.Unk22), "Unused categories are outside the filter.");
Check(ReplacementPolicy.IsAlliedCategory(AILordMessageType.MerryChristmas) && ReplacementPolicy.IsAlliedCategory(AILordMessageType.WillAttackEnemy), "Allied category endpoints must be included.");
Check(!ReplacementPolicy.ShouldReplace(AILords.SK_RAT, AILordMessageType.RequestGoods, audio, video, null, video), "Missing media is not proof of a placeholder.");
Check(!ReplacementPolicy.ShouldReplace(AILords.SK_RAT, AILordMessageType.RequestGoods, "fx/speech/rt_req.wav", video, "fx/speech/rt_req.wav", video), "An individual voice is not generic just because its video is generic.");
Console.WriteLine("PASS: " + checks + " production replacement-policy checks.");
