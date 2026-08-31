using System;

namespace CustomLordUpload
{
    internal sealed class CustomLordUploadIssue
    {
        internal CustomLordUploadIssue(string code, params object[] replacements)
            : this(code, string.Empty, replacements)
        {
        }

        internal CustomLordUploadIssue(string code, string technicalDetail, params object[] replacements)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("An issue code is required.", nameof(code));

            Code = code;
            TechnicalDetail = technicalDetail ?? string.Empty;
            Replacements = replacements ?? Array.Empty<object>();
        }

        internal string Code { get; }
        internal string LocalizationKey => "CustomLordUpload." + Code;
        internal string TechnicalDetail { get; }
        internal object[] Replacements { get; }

        internal string Format()
        {
            return SerpLocalization.Get(LocalizationKey, Replacements);
        }
    }
}
