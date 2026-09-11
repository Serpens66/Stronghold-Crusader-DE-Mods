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
            int? insertionIndex,
            int? colourId,
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

            if (colourId.HasValue)
                member.colourID = colourId.Value;

            if (insertionIndex.HasValue)
            {
                // Coop rebuilds the partner first. Preserve that list position so mapping remains player slot 2.
                owner.currentLobby.members.Remove(member);
                int index = Math.Max(0, Math.Min(insertionIndex.Value, owner.currentLobby.members.Count));
                owner.currentLobby.members.Insert(index, member);
            }

            owner.currentLobby.numLobbyMembers = owner.currentLobby.members.Count;
            UpdateSteamMappings(owner);
            FinalizeIdentity(owner, member);
            playerId = owner.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
            if (playerId < 1 || playerId > owner.AIVs.Length)
                throw new InvalidOperationException("The custom lord has no valid lobby player slot.");

            InitializeAivInfo(owner.AIVs[playerId - 1], member, lord);
            return member;
        }

        internal static bool IsValid(CustomisationFileManager.CustomLord lord) =>
            lord != null &&
            !string.IsNullOrWhiteSpace(lord.lordName) &&
            lord.aivs != null && lord.aivs.Count > 0 &&
            lord.configs != null && lord.configs.Count > 0;

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
