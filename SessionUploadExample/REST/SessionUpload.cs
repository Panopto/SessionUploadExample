using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SessionUploadExample
{
    /// <summary>
    /// An upload object for ingesting an entire session consisting of one or more files into a folder.
    /// </summary>
    [DataContract]
    public class SessionUpload : Upload
    {
        /// <summary>
        /// The id of the folder to upload the new session to. Required field when creating the session upload.
        /// </summary>
        [DataMember]
        public Guid FolderId { get; set; }

        /// <summary>
        /// The id of the session that was created for this upload.
        /// Only populated after a session has been created in the Panopto system during the Processing state.
        /// </summary>
        [DataMember]
        public Guid? SessionId { get; set; }
    }
}
