# Custom Lord title uses the player slot instead of the lord subtype

## Current behavior

`ManagedHooks/OnScreenText_Hooks.cs` resolves the extended player slot and passes that value to `GetLordTitle`:

    GetSlotIndexByExtendedLordEnum(lord, out int internalLordId);
    lordName = GetGenericSlotLordName(internalLordId);
    return displayName + GetLordTitle(lordName, internalLordId);

This selects titles by player position. It can repeat the same title and does not match Vanilla's eight per-lord subtype slots.

## Suggested fix

Use the existing `computerName` argument as the title index:

    return displayName + GetLordTitle(lordName, computerName);

`computerName` is the Vanilla lord subtype (`0..7`), while `internalLordId` is still correct for resolving the Custom Lord assigned to the extended player slot.
