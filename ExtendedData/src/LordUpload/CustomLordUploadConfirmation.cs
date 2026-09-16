using CrusaderDE;
using System;
using System.Collections.Generic;
using System.Text;

namespace ExtendedData
{
    internal sealed class CustomLordUploadConfirmation : ICustomLordUploadConfirmation
    {
        public void Show(
            IReadOnlyList<CustomLordUploadIssue> issues,
            Action confirm,
            Action cancel)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine(SerpLocalization.Get("ExtendedData.LordUpload.WarningIntro"));
            message.AppendLine();
            for (int index = 0; index < issues.Count; index++)
                message.Append(index + 1).Append(". ").AppendLine(issues[index].Format());
            message.AppendLine();
            message.Append(SerpLocalization.Get("ExtendedData.LordUpload.UploadAnyway"));

            // COMPATIBILITY: Recheck this popup signature and callback semantics after game-DLL updates.
            HUD_ConfirmationPopup.ShowConfirmationMessage(
                SerpLocalization.Get("ExtendedData.LordUpload.WarningTitle"),
                confirm,
                cancel,
                message.ToString(),
                MPConf: false,
                tall: true);
        }
    }
}
