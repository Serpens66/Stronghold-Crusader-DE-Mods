using CrusaderDE;
using System;

namespace CustomLordUpload
{
    internal sealed class CustomLordUploadRequest
    {
        internal CustomLordUploadRequest(
            Platform_Workshop instance,
            string nameMap,
            string mapTitle,
            string description,
            string[] tags,
            bool publicMap,
            string previewImage,
            Action successAction,
            Action failAction,
            bool includeAdditionalFiles = true)
        {
            Instance = instance;
            NameMap = nameMap;
            MapTitle = mapTitle;
            Description = description;
            Tags = tags;
            PublicMap = publicMap;
            PreviewImage = previewImage;
            SuccessAction = successAction;
            FailAction = failAction;
            IncludeAdditionalFiles = includeAdditionalFiles;
        }

        internal Platform_Workshop Instance { get; }
        internal string NameMap { get; }
        internal string MapTitle { get; }
        internal string Description { get; }
        internal string[] Tags { get; }
        internal bool PublicMap { get; }
        internal string PreviewImage { get; }
        internal Action SuccessAction { get; }
        internal Action FailAction { get; }
        internal bool IncludeAdditionalFiles { get; }

        internal CustomLordUploadRequest WithCallbacks(Action success, Action failure)
        {
            return new CustomLordUploadRequest(
                Instance, NameMap, MapTitle, Description, Tags, PublicMap,
                PreviewImage, success, failure, IncludeAdditionalFiles);
        }
    }
}
