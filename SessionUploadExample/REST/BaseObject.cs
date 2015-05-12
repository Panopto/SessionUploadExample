using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SessionUploadExample
{
    /// <summary>
    /// A simple object used to convey messages back to the caller.
    /// </summary>
    [DataContract]
    public class BaseObject
    {
        /// <summary>
        /// ID of this object.
        /// </summary>
        [DataMember(
            IsRequired = false,
            EmitDefaultValue = false)]
        public Guid ID;

        /// <summary>
        /// Gets or sets the supplementary status message ID for this object.
        /// </summary>
        [DataMember(
            IsRequired = false,
            EmitDefaultValue = false)]
        public int MessageID { get; set; }

        /// <summary>
        /// Gets or sets the supplementary status message text for this object.
        /// </summary>
        [DataMember(
            IsRequired = false,
            EmitDefaultValue = false)]
        public string Message { get; set; }
    }
}
