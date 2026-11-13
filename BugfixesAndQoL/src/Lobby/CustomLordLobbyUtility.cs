// Shared Vanilla-compatible initialization for custom AI lobby members.
using CrusaderDE;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal static class CustomLordLobbyUtility
    {
        private static readonly MethodInfo UpdateSteamMappingsMethod =
            typeof(FRONT_Multiplayer).GetMethod(
                "updateSteamIDMappings",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, "updateSteamIDMappings");

        internal static Platform_Multiplayer.MPLobbyMember AddAndInitialize(
            FRONT_Multiplayer owner,
            CustomisationFileManager.CustomLord lord,
            int forcedTeam,
            out int playerId)
        {
            playerId = 0;
            if (owner?.currentLobby?.members == null)
                throw new InvalidOperationException("The custom-lord lobby is unavailable.");
            if (!IsValid(lord))
                throw new InvalidOperationException("The custom lord has no usable AIV/AIC data.");

            Platform_Multiplayer.MPLobbyMember member =
                Platform_Multiplayer.Instance.AddCustomSkirmishPlayerLocal(lord, forcedTeam);
            if (member == null)
                throw new InvalidOperationException("Vanilla did not create the custom-lord lobby member.");

            owner.currentLobby.numLobbyMembers = owner.currentLobby.members.Count;
            UpdateSteamMappings(owner);
            FinalizeIdentity(owner, member);
            playerId = owner.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
            if (playerId < 1 || playerId > owner.AIVs.Length)
                throw new InvalidOperationException("The custom lord has no valid lobby player slot.");

            InitializeAivInfo(owner.AIVs[playerId - 1], member, lord);
            return member;
        }

        internal static Platform_Multiplayer.MPLobbyMember ConfigureExistingCoopPartner(
            Platform_Multiplayer.MPLobby lobby,
            FRONT_Multiplayer.MPAIVInfo[] infos,
            CustomisationFileManager.CustomLord lord)
        {
            Platform_Multiplayer.MPLobbyMember partner = RequireCoopPartner(lobby, infos);
            if (!IsValid(lord))
                throw new InvalidOperationException("The custom lord has no usable AIV/AIC data.");

            // Prepare off to the side: a failed import must not change the existing member or selection.
            var prepared = new FRONT_Multiplayer.MPAIVInfo();
            InitializeAivInfo(prepared, partner, lord);

            // Keep Vanilla's identity and mapping. Removing this member would discard its mission keep
            // assignment; updateSteamIDMappings would then choose a random free map position.
            partner.customLordName = lord.lordName;
            infos[1] = prepared;
            return partner;
        }

        internal static void ValidateSelectedCoopPartner(
            Platform_Multiplayer.MPLobby lobby,
            FRONT_Multiplayer.MPAIVInfo[] infos,
            string selectedLordName)
        {
            Platform_Multiplayer.MPLobbyMember partner = RequireCoopPartner(lobby, infos);
            if (string.IsNullOrWhiteSpace(selectedLordName) ||
                !string.Equals(partner.customLordName, selectedLordName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(infos[1]?.lordName, selectedLordName, StringComparison.OrdinalIgnoreCase))
                throw new Shared.CoopAiSelectionException("InvalidCustomLord", 2,
                    "selected Lord, partner identity and AIV selection disagree");
            Shared.CoopAiStartPlanFactory.Validate(infos[1], 2, partner.GetLordType(), out _);
        }

        private static Platform_Multiplayer.MPLobbyMember RequireCoopPartner(
            Platform_Multiplayer.MPLobby lobby,
            FRONT_Multiplayer.MPAIVInfo[] infos)
        {
            if (lobby?.members == null || lobby.this_player_to_SteamID_mapping?.Length != 8 ||
                infos?.Length != 8)
                throw new Shared.CoopAiSelectionException("InvalidSlot", 2, "eight-slot Coop lobby unavailable");

            ulong partnerId = lobby.this_player_to_SteamID_mapping[1];
            int mappings = 0;
            foreach (ulong id in lobby.this_player_to_SteamID_mapping)
                if (id == partnerId) mappings++;
            Platform_Multiplayer.MPLobbyMember partner = null;
            int members = 0;
            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
            {
                if (member == null || member.GetSteamID() != partnerId) continue;
                partner = member;
                members++;
            }
            if (partnerId == 0 || mappings != 1 || members != 1 ||
                !partner.SkirmishMember || partner.SkirmishHumanMember ||
                partner.GetLordType() != CoopCustomLordSelectionPolicy.CustomPartnerLordType ||
                lobby.getTeam(partner) < 0)
                throw new Shared.CoopAiSelectionException("InvalidSlot", 2,
                    "Coop partner identity, mapping or team is missing or inconsistent");
            return partner;
        }

        internal static bool IsValid(CustomisationFileManager.CustomLord lord) =>
            lord != null &&
            !string.IsNullOrWhiteSpace(lord.lordName) &&
            lord.aivs != null && lord.aivs.Count > 0 &&
            lord.aivs[0]?.data != null && lord.aivs[0].data.Length > 0 &&
            lord.configs != null && lord.configs.Count > 0 && lord.configs[0] != null;

        internal static void UpdateSteamMappings(FRONT_Multiplayer owner) =>
            UpdateSteamMappingsMethod.Invoke(owner, null);

        internal static void FinalizeIdentity(
            FRONT_Multiplayer owner,
            Platform_Multiplayer.MPLobbyMember member)
        {
            ulong[] mappings = owner.currentLobby.this_player_to_SteamID_mapping;
            for (int slotIndex = 0; slotIndex < mappings.Length; slotIndex++)
            {
                if (mappings[slotIndex] != member.GetSteamID())
                    continue;

                ulong temporarySteamId = member.GetSteamID();
                member.SetValidCustomLordType(slotIndex, member.GetLordSubType());
                mappings[slotIndex] = member.GetSteamID();
                owner.currentLobby.switchTeamID(temporarySteamId, member.GetSteamID());
                return;
            }

            throw new InvalidOperationException("The custom-lord Steam mapping could not be finalized.");
        }

        internal static void InitializeAivInfo(
            FRONT_Multiplayer.MPAIVInfo info,
            Platform_Multiplayer.MPLobbyMember member,
            CustomisationFileManager.CustomLord lord)
        {
            info.Init(member.GetLordType(), lord.lordName);
            info.lordConfig = lord.configs[0];
            info.aivs.Add(lord.aivs[0]);
            info.imageData = lord.imageData;
            info.image = lord.image;
        }
    }
}
