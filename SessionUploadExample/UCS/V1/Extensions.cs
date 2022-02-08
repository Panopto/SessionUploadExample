using System.Collections.Generic;
using System.Linq;

namespace SessionUploadExample.UCS.V1
{
    public partial class PanoptoSession
    {
        public IEnumerable<string> GetReferencedFiles()
        {
            List<string> videoFiles = this?.Videos
                ?.Select(video => video.Filename.Value)
                ?.ToList() ?? new List<string>();
            List<string> transcriptFiles = this?.Videos
                ?.SelectMany(video => video.Transcripts)
                ?.Select(transcript => transcript.Filename.Value)
                ?.ToList() ?? new List<string>();
            List<string> presentationFiles = this?.Presentations
                ?.Select(presentation => presentation.Filename.Value)
                .ToList() ?? new List<string>();
            List<string> imageFiles = this?.Images
                ?.Select(image => image.Filename.Value)
                ?.ToList() ?? new List<string>();
            List<string> attachmentFiles = this?.Attachments
                ?.Select(attachment => attachment.Filename.Value)
                ?.ToList() ?? new List<string>();

            return videoFiles.Concat(transcriptFiles).Concat(presentationFiles).Concat(imageFiles).Concat(attachmentFiles);
        }
    }
}
