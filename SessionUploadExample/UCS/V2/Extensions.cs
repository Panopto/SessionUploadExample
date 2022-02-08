using System.Collections.Generic;
using System.Linq;

namespace SessionUploadExample.UCS.V2
{
    public partial class Session
    {
        public IEnumerable<string> GetReferencedFiles()
        {
            List<string> thumbnail = this.Thumbnail?.Value != null
                ? new List<string> { this.Thumbnail.Value }
                : new List<string>();
            List<string> videoFiles = this.Videos
                ?.Select(video => video.File.Value)
                ?.ToList() ?? new List<string>();
            List<string> transcriptFiles = this.Videos
                ?.SelectMany(video => video.Transcripts)
                ?.Select(transcript => transcript.File.Value)
                ?.ToList() ?? new List<string>();
            List<string> presentationFiles = this.Presentations
                ?.Select(presentation => presentation.File.Value)
                ?.ToList() ?? new List<string>();
            List<string> imageFiles = this.Images
                ?.Select(image => image.File.Value)
                ?.ToList() ?? new List<string>();
            List<string> attachmentFiles = this.Attachments
                ?.Select(attachment => attachment.File.Value)
                ?.ToList() ?? new List<string>();

            return thumbnail.Concat(videoFiles).Concat(transcriptFiles).Concat(presentationFiles).Concat(imageFiles).Concat(attachmentFiles);
        }
    }
}
