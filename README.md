# SessionUploadExample
Sample code that shows how to upload complete multi-stream sessions to Panopto.

Learn more here:
http://panopto.com/features/universal-capture-specification/

The [UCS code files](SessionUploadExample/UCS) were generated via the following commands:
```
xsd Universal-Capture-v1_0.xsd /classes /language:CS /namespace:SessionUploadExample.UCS.V1 /out:SessionUploadExample\UCS\V1\
xsd Universal-Capture-v2_0.xsd /classes /language:CS /namespace:SessionUploadExample.UCS.V2 /out:SessionUploadExample\UCS\V2\
```

Sample session XMLs can be found in the "Tests" directory.

SESSION UPLOAD API
Used to create and upload a new session with videos, presentations, cuts, transcripts.

URL: http://<server>/Panopto/Services/PublicAPI/REST/sessionUpload

REST object:

SessionUpload
{

    // The id of the folder to upload the new session to. Required field when creating the session upload.
    Guid FolderId;

    // The id of the session that was created for this upload.
    // Only populated after a session has been created in the Panopto system during the Processing state.
    Guid? SessionId;
    
    // The location where session and video files should be added.
    string UploadTarget;

    // Gets or sets the current state for this upload.
    // Set this field to UploadComplete to signal that the user is done uploading files.
    UploadStatus State;

    // The priority at which to process the upload.
    UploadPriority Priorty;
    
    // ID of this object.
    Guid ID;

    // A supplementary status message ID for this object.
    int MessageID;

    // A supplementary status message text for this object.
    string Message;
}

enum UploadStatus
{

    // An upload was created and cancel nor complete have been called yet, nor has it timed out.
    Uploading = 0,

    // The upload has been completed but has yet to be transferred to storage.
    UploadComplete = 1,

    // The upload was cancelled by the user and hasn't been deleted yet.
    UploadCancelled = 2,

    // The upload is being processed.
    Processing = 3,

    // The upload has been processed and can now be used by the Panopto system.
    Complete = 4,

    // There was an error during transfer.
    ProcessingError = 5,

    // The upload was cancelled or timed out and is being deleted.
    DeletingFiles = 6,

    // The upload was cancelled or timed out and was deleted.
    Deleted = 7,

    // There was an error while deleting the upload.
    DeletingError = 8
}


To initiate the upload, make an HTTP POST with a JSON body of a SessionUpload object with the FolderId field populated. This is the folder id to create a new session in. The folder id can be found via the SessionManagement.GetFoldersList API: https://support.panopto.com/resource/APIDocumentation/Help/index.html (Panopto.Server.Services.PublicAPI.V46.Soap -> ISessionManagement Interface -> ISessionManagement Methods -> GetFoldersList)

The response to the HTTP POST will contain a URL in UploadTarget, as well as an ID to use for subsequent REST calls.

Use the AMAZON S3 protocol to upload the file to the Panopto server at the location specified by UploadTarget. The AWS S3 library is compatible with Panopto servers: http://docs.aws.amazon.com/AmazonS3/latest/API/APIRest.html

Upload all files that are referenced in the session XML and also the XML file itself. Once finished, make an HTTP PUT call with a SessionUpload object with the UploadStatus set to UploadComplete. This will signal to the server that the session is ready for processing.

The status of processing can be monitored via HTTP GET calls using the ID field.

The upload can be cancelled at any time via an HTTP DELETE call using the ID field.


STREAM UPLOAD API
Used to add videos and presentations to an existing session.

URL: http://<server>/Panopto/Services/PublicAPI/REST/upload

REST object:

class StreamUpload
{

    // Gets or sets the session ID associated with this upload.
    Guid SessionID;

    // Gets or sets the stream type for this stream.
    // If unspecified, the stream is assumed to be
    // the primary video.
    StreamType? StreamType;

    // Get or set the absolute start time for this stream.  If
    // not specified, the stream is assumed to start at offset
    // 0s relative to the other streams in this session, or--if there 
    // are no other streams--at the time this API is called.
    // 
    // DateTime should be specified in UTC.
    DateTime? StreamStartTime;

    // Unison import job ID associated with the uploaded file(s).
    Guid? ImportJobID;
}

enum StreamType
{

    // Primary video stream.
    PrimaryVideo,

    // Secondary video stream.
    SecondaryVideo,

    // Secondary presentation stream.
    SecondaryPresentation,
}


To initiate the upload, make an HTTP POST with a JSON body of a StreamUpload object with the SessionId field populated. The StreamType and StreamStartTime fields are optional. This is the session id to create a new stream on. The session id can be found via the SessionManagement.GetSessionsList API: https://support.panopto.com/resource/APIDocumentation/Help/index.html (Panopto.Server.Services.PublicAPI.V46.Soap -> ISessionManagement Interface -> ISessionManagement Methods -> GetSessionsList)

The response to the HTTP POST will contain a URL in UploadTarget, as well as an ID to use for subsequent REST calls.

Use the AMAZON S3 protocol to upload the file to the Panopto server at the location specified by UploadTarget. The AWS S3 library is compatible with Panopto servers: http://docs.aws.amazon.com/AmazonS3/latest/API/APIRest.html

Upload the stream file. Once finished, make an HTTP PUT call with a StreamUpload object with the UploadStatus set to UploadComplete. This will signal to the server that the stream is ready for processing.

The status of processing can be monitored via HTTP GET calls using the ID field.

The upload can be cancelled at any time via an HTTP DELETE call using the ID field.
