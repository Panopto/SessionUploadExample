using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SessionUploadExample
{
    /// <summary>
    /// An upload object for ingesting one or more files into the Panopto system.
    /// </summary>
    [DataContract]
    public class Upload : BaseObject
    {
        /// <summary>
        /// Gets or sets the location where session and video files should be added.
        /// </summary>
        [DataMember(
            IsRequired = false,
            EmitDefaultValue = false)]
        public string UploadTarget { get; set; }

        /// <summary>
        /// Gets or sets the current state for this upload.
        /// </summary>
        [DataMember(
            IsRequired = false,
            EmitDefaultValue = false)]
        public UploadStatus State { get; set; }
    }
}
