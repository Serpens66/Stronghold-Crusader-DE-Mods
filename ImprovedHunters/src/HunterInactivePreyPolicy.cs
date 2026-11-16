using System;
using APIShared.Wildlife;

namespace ImprovedHunters
{
    internal static class HunterInactivePreyPolicy
    {
        internal static bool ShouldRefuse(int type, HuntablePreyRegistration own, HuntablePreyCatalog catalog) =>
            own != null && catalog != null && catalog.TryGet(type, out var admitted) &&
            IsOwnAdmission(own.OwnerGuid, own.RegistrationId, admitted.OwnerGuid, admitted.RegistrationId);
        // Enabled flags can change before a frozen catalog does. Only the exact admitted
        // registration belongs to this mod; a same-type winner from another owner does not.
        internal static bool IsOwnAdmission(string owner, string registration, string admittedOwner, string admittedRegistration) =>
            !string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(registration) &&
            string.Equals(owner, admittedOwner, StringComparison.Ordinal) &&
            string.Equals(registration, admittedRegistration, StringComparison.Ordinal);
    }
}
