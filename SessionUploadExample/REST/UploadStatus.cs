using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SessionUploadExample
{
    /// <summary>
    /// Various states for an upload operation through the upload api.
    /// </summary>
    [DataContract]
    public enum UploadStatus
    {
        /// <summary>
        /// An upload was created and cancel nor complete have been called yet, nor has it timed out.
        /// </summary>
        [EnumMember]
        Uploading = 0,

        /// <summary>
        /// The upload has been completed but has yet to be transferred to storage.
        /// </summary>
        [EnumMember]
        UploadComplete = 1,

        /// <summary>
        /// The upload was cancelled by the user and hasn't been deleted yet.
        /// </summary>
        [EnumMember]
        UploadCancelled = 2,

        /// <summary>
        /// The upload is being processed.
        /// </summary>
        [EnumMember]
        Processing = 3,

        /// <summary>
        /// The upload has been processed and can now be used by the Panopto system.
        /// </summary>
        [EnumMember]
        Complete = 4,

        /// <summary>
        /// There was an error during transfer.
        /// </summary>
        [EnumMember]
        ProcessingError = 5,

        /// <summary>
        /// The upload was canceled or timed out and is being deleted.
        /// </summary>
        [EnumMember]
        DeletingFiles = 6,

        /// <summary>
        /// The upload was canceled or timed out and was deleted.
        /// </summary>
        [EnumMember]
        Deleted = 7,

        /// <summary>
        /// There was an error while deleting the upload.
        /// </summary>
        [EnumMember]
        DeletingError = 8
    }
}
