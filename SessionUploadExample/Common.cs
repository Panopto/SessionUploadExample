using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Amazon.S3;
using Amazon.S3.Model;

namespace SessionUploadExample
{
    public class Common
    {
        /// <summary>
        /// Number of bytes in a kilobyte.
        /// </summary>
        public const long KiB = 1024;

        /// <summary>
        /// Number of bytes in a megabyte.
        /// </summary>
        public const long MiB = 1024 * KiB;

        /// <summary>
        /// Number of bytes in a gigabyte.
        /// </summary>
        public const long GiB = 1024 * MiB;

        /// <summary>
        /// A format string for accessing the Panopto REST API.
        /// </summary>
        private const string UriStemFormat = "https://{0}/Panopto/PublicAPI/REST";
        
        /// <summary>
        /// The cookie name for the auth cookie sent back from a Panopto server in response to a sucessful log in.
        /// </summary>
        private const string AuthCookieName = ".ASPXAUTH";

        /// <summary>
        /// The location to recieve file uploads for a Panopto server.
        /// </summary>
        private const string UploadBucketName = "Upload";

        /// <summary>
        /// A fragment of the upload path containing the root folder.
        /// </summary>
        private const string UploadTargetPathFragment_Panopto = "/Panopto/";
        
        /// <summary>
        /// A fragment of the upload path containing the root folder and the upload bucket folder.
        /// </summary>
        private const string UploadTargetPathFragment_PanoptoUpload = "/Panopto/Upload/";

        /// <summary>
        /// Specifies that SSL errors while connecting to a Panopto server should be ignored.
        /// </summary>
        public static bool IgnoreSSLErrors
        {
            get;
            set;
        }

        /// <summary>
        /// Serialize an object in JSON format.
        /// </summary>
        /// <param name="item">Object to serialize.</param>
        /// <returns>JSON string representation of the object.</returns>
        public static string SerializeAsJson(
            object item)
        {
            return new JavaScriptSerializer().Serialize(item);
        }

        /// <summary>
        /// Use the Auth API to log on to the server.
        /// </summary>
        /// <param name="userKey">User key.</param>
        /// <param name="password">Password.</param>
        /// <returns>Auth cookie.</returns>
        public static string LogonAndGetCookie(
            string userKey,
            string password,
            string serverDns)
        {
            string result = null;

            if (Common.IgnoreSSLErrors)
            {
                // ignore SSL errors
                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => { return true; };
            }

            // set up the request
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(
                "https://" + serverDns + "/Panopto/PublicAPI/4.2/Auth.svc");

            request.Headers.Add("SOAPAction", "http://tempuri.org/IAuth/LogOnWithPassword");

            request.Method = "POST";

            request.ContentType = "text/xml; charset=utf-8";

            string bodyString = string.Format(
                "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body><LogOnWithPassword xmlns=\"http://tempuri.org/\"><userKey>{0}</userKey><password>{1}</password></LogOnWithPassword></s:Body></s:Envelope>",
                userKey,
                password);

            byte[] buffer = Encoding.UTF8.GetBytes(bodyString);

            request.ContentLength = buffer.Length;

            string setCookies = null;

            // send off the request
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(buffer, 0, buffer.Length);
            }

            // get the repsonse
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                setCookies = response.Headers["Set-Cookie"];
            }

            // parse the response, a series of cookies which are ;-delimited
            if (!string.IsNullOrEmpty(setCookies))
            {
                //get to the start
                setCookies = setCookies.Substring(setCookies.IndexOf(Common.AuthCookieName) + Common.AuthCookieName.Length + 1);

                // trim the end
                int semiIndex = setCookies.IndexOf(";");

                result = setCookies.Substring(0, semiIndex > -1 ? semiIndex : setCookies.Length);
            }

            return result;
        }

        /// <summary>
        /// Create an HttpWebRequest object from the constituent parts.
        /// </summary>
        /// <param name="method">HTTP method to use.</param>
        /// <param name="uriLeaf">Leaf portion of the URI to query.</param>
        /// <param name="authCookie">Auth cookie.  Use the value returned from LogonAndGetCookie.</param>
        /// <param name="serverDns">The domain name for the server to connect to.</param>
        /// <param name="requestBody">Body of the request.  May be null.</param>
        /// <returns>The created web request object.</returns>
        public static HttpWebRequest CreateRequest(
            string method,
            string uriLeaf,
            string authCookie,
            string serverDns,
            string requestBody = null)
        {
            if (Common.IgnoreSSLErrors)
            {
                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => { return true; };
            }

            string uri;
            if (string.IsNullOrWhiteSpace(uriLeaf))
            {
                uri = string.Format(Common.UriStemFormat, serverDns);
            }
            else
            {
                uri = string.Format("{0}/{1}", string.Format(Common.UriStemFormat, serverDns), uriLeaf);
            }

            HttpWebRequest result = (HttpWebRequest)WebRequest.Create(uri);

            result.Method = method;

            result.ContentType = "application/json; charset=utf-8";

            if (!string.IsNullOrWhiteSpace(authCookie))
            {
                // add the auth cookie
                result.CookieContainer = new CookieContainer();
                Cookie cookie = new Cookie(Common.AuthCookieName, authCookie);
                cookie.Domain = serverDns;
                result.CookieContainer.Add(cookie);
            }

            if (requestBody == null)
            {
                result.ContentLength = 0;
            }
            else
            {
                using (StreamWriter writer = new StreamWriter(result.GetRequestStream()))
                {
                    writer.Write(requestBody);
                }
            }
            return result;
        }

        /// <summary>
        /// Parse a JSON string into the desired object type.
        /// </summary>
        /// <typeparam name="T">Resultant type.</typeparam>
        /// <param name="responseBody">Text to parse.</param>
        /// <returns>New instance of the desired object type or null if a null string was passed in.</returns>
        public static T ParseResponseBody<T>(
            string responseBody) where T : class, new()
        {
            if (responseBody == null)
            {
                return null;
            }

            return new JavaScriptSerializer().Deserialize<T>(responseBody);
        }

        /// <summary>
        /// Get the response body from a web exception.
        /// </summary>
        /// <param name="exception">Exception object.</param>
        /// <returns>Response body, if any.</returns>
        public static string GetResponseBody(
            WebException exception)
        {
            if (exception == null)
            {
                return null;
            }

            return GetResponseBody(exception.Response);
        }

        /// <summary>
        /// Get the response body from a web response object.
        /// </summary>
        /// <param name="response">Web response object.</param>
        /// <returns>Response body, if any.</returns>
        public static string GetResponseBody(
            WebResponse response)
        {
            if (response == null)
            {
                return null;
            }

            if (response.ContentLength == 0)
            {
                return null;
            }

            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Assert that a response body is parsable as the desired type.
        /// </summary>
        /// <typeparam name="T">Expected object type.</typeparam>
        /// <param name="responseBody">Response body text.</param>
        public static void AssertResponseBodyParsable<T>(
            string responseBody) where T : class, new()
        {
            try
            {
                T parsedBody = ParseResponseBody<T>(responseBody);
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "Failed to parse response body as type {0}.\n" +
                    "Exception: {1}\n" +
                    "Response body:\n" +
                    "{2}",
                    typeof(T).AssemblyQualifiedName,
                    e.ToString(),
                    responseBody);

                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Get the response from a web request, parsed from JSON into
        /// the desired object type.
        /// </summary>
        /// <typeparam name="T">Desired object type.</typeparam>
        /// <param name="expectedHttpStatusCode">Expected HTTP status code returned
        /// for this request.</param>
        /// <param name="request">Request object to submit.</param>
        /// <param name="parseBody">True if the response body should be parsed as
        /// the desired object type.</param>
        /// <returns>Instance of the desired object.</returns>
        public static T GetResponse<T>(
            HttpStatusCode expectedHttpStatusCode,
            HttpWebRequest request,
            bool parseBody = true) where T : class, new()
        {
            if (Common.IgnoreSSLErrors)
            {
                ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => { return true; };
            }

            T result = null;

            try
            {
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;

                string responseBody = GetResponseBody(response);

                if (expectedHttpStatusCode != response.StatusCode)
                {
                    Console.WriteLine(
                        "Expected status code does not match actual status code.\n" +
                        "Response body:\n" +
                        "{0}",
                        responseBody);

                    Environment.Exit(1);
                }

                if (parseBody)
                {
                    AssertResponseBodyParsable<T>(responseBody);

                    result = ParseResponseBody<T>(responseBody);
                }
            }
            catch (System.Net.WebException e)
            {
                HttpWebResponse response = e.Response as HttpWebResponse;

                if (response != null)
                {
                    string responseBody = GetResponseBody(response);

                    if (expectedHttpStatusCode != response.StatusCode)
                    {
                        Console.WriteLine(
                            "Expected status code does not match actual status code.\n" +
                            "Response body:\n" +
                            "{0}",
                            responseBody);

                        Environment.Exit(1);
                    }

                    if (parseBody)
                    {
                        AssertResponseBodyParsable<T>(responseBody);

                        result = ParseResponseBody<T>(responseBody);
                    }
                }
                else
                {
                    Console.WriteLine("Error contacting Panopto server: {0}", e.ToString());
                }
            }

            return result;
        }

        /// <summary>
        /// Create a configured S3Client pointing at localhost to test this server
        /// </summary>
        /// <param name="uploadTarget">Upload target returned from the Upload REST API.</param>
        /// <param name="accessKeyId">S3 user ID.  Value is unused for our S3
        /// minimialist server.</param>
        /// <param name="secretAccessKey">S3 access key.  Value is unused for our
        /// S3 minimalist server.</param>
        /// <returns>A new S3 client.</returns>
        public static AmazonS3Client CreateS3Client(
            string uploadTarget,
            string accessKeyId = "foo",
            string secretAccessKey = "bar")
        {
            Uri serviceUri = new Uri(GetServiceUrlFromUploadTarget(uploadTarget));

            AmazonS3Config s3Config = new AmazonS3Config()
            {
                // Amazon SDK will append uploadBucketName ("Upload") to the path and hence hit the actual service endpoint
                ServiceURL = serviceUri.AbsoluteUri,
                UseHttp = serviceUri.Scheme == "http",
            };

            AmazonS3Client s3Client = new AmazonS3Client(
                accessKeyId,
                secretAccessKey,
                s3Config);

            return s3Client;
        }

        /// <summary>
        /// Open an upload request with the server.
        /// </summary>
        /// <param name="s3Client">S3 client object.</param>
        /// <param name="uploadTarget">Upload target returned from the Upload REST API.</param>
        /// <param name="fileName">Name of the file to be uploaded.</param>
        /// <returns>Response from S3 server.  Response includes the UploadId which
        /// is used in subsequent S3 server calls.</returns>
        public static InitiateMultipartUploadResponse OpenUpload(
            AmazonS3Client s3Client,
            string uploadTarget,
            string fileName)
        {
            string fileKey = GetFileKey(uploadTarget, fileName);
            InitiateMultipartUploadRequest initiateRequest = new InitiateMultipartUploadRequest()
            {
                BucketName = Common.UploadBucketName,
                Key = fileKey,
            };

            //
            // AWS SDK for .NET 3.5 default content-length is -1.  This results in the content-length header
            // not being sent; the request is rejected by the server.
            // AWS SDK for .NET 4.5 default content-length is 0.
            // Set this to zero so it's always valid.
            //
            initiateRequest.Headers.ContentLength = 0;
            return s3Client.InitiateMultipartUpload(initiateRequest);
        }

        /// <summary>
        /// Send up the file in chunks of partSize bytes
        /// </summary>
        /// <param name="s3Client">S3 client object.</param>
        /// <param name="uploadTarget">Upload target returned from the Upload REST API.</param>
        /// <param name="fileName">Name of the file to be uploaded.</param>
        /// <param name="uploadId">Upload ID returned from the S3 server.</param>
        /// <param name="partSize">Size of chunks to upload.</param>
        /// <returns>List of upload part responses from the server.</returns>
        public static List<UploadPartResponse> UploadParts(
            AmazonS3Client s3Client,
            string uploadTarget,
            string fileName,
            string uploadId,
            long partSize)
        {
            long fileSize = new System.IO.FileInfo(fileName).Length;
            string fileKey = GetFileKey(uploadTarget, fileName);

            List<UploadPartResponse> uploadResponses = new List<UploadPartResponse>();
            long filePosition = 0;
            for (int i = 1; filePosition < fileSize; i++)
            {
                UploadPartRequest uploadRequest = new UploadPartRequest
                {
                    BucketName = Common.UploadBucketName,
                    Key = fileKey,
                    UploadId = uploadId,
                    PartNumber = i,
                    PartSize = partSize,
                    FilePosition = filePosition,
                    FilePath = fileName
                };

                // add the response to the list since it will be needed to complete the upload
                uploadResponses.Add(s3Client.UploadPart(uploadRequest));

                filePosition += partSize;
            }

            return uploadResponses;
        }

        /// <summary>
        /// Signal the upload for this uploadId and fileName is complete
        /// </summary>
        /// <param name="s3Client">S3 client object.</param>
        /// <param name="uploadTarget">Upload target returned from the Upload REST API.</param>
        /// <param name="fileName">Name of the file to be uploaded.</param>
        /// <param name="uploadId">Upload ID returned from the S3 server.</param>
        /// <param name="partResponses">List of upload part responses from the server.</param>
        /// <returns>Response from S3 server.</returns>
        public static CompleteMultipartUploadResponse CloseUpload(
            AmazonS3Client s3Client,
            string uploadTarget,
            string fileName,
            string uploadId,
            List<UploadPartResponse> partResponses)
        {
            string fileKey = GetFileKey(uploadTarget, fileName);
            CompleteMultipartUploadRequest completeRequest = new CompleteMultipartUploadRequest
            {
                BucketName = Common.UploadBucketName,
                Key = fileKey,
                UploadId = uploadId,
            };
            
            completeRequest.AddPartETags(partResponses);

            return s3Client.CompleteMultipartUpload(completeRequest);
        }

        /// <summary>
        /// Tell the server to cancel the upload with this filename and uploadId
        /// </summary>
        /// <param name="s3Client">S3 client object.</param>
        /// <param name="uploadTarget">Upload target returned from the Upload REST API.</param>
        /// <param name="fileName">Name of the file to be uploaded.</param>
        /// <param name="uploadId">Upload ID returned from the S3 server.</param>
        /// <returns>Response from S3 server.</returns>
        public static AbortMultipartUploadResponse AbortUpload(
            AmazonS3Client s3Client,
            string uploadTarget,
            string fileName,
            string uploadId)
        {
            string fileKey = GetFileKey(uploadTarget, fileName);
            AbortMultipartUploadRequest abortMPURequest = new AbortMultipartUploadRequest
            {
                BucketName = Common.UploadBucketName,
                Key = fileKey,
                UploadId = uploadId
            };
            return s3Client.AbortMultipartUpload(abortMPURequest);
        }

        /// <summary>
        /// Use the S3 protocol to upload the local file at filePath to the server at uploadTarget.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="uploadTarget"></param>
        public static void TranslocateFile(string uploadTarget, string filePath)
        {
            // Create an S3 client.
            AmazonS3Client s3Client = Common.CreateS3Client(
                uploadTarget);

            // Initiate an upload to the S3 server.
            InitiateMultipartUploadResponse initMultiPartUploadResponse = Common.OpenUpload(
                s3Client,
                uploadTarget,
                filePath);
            string uploadId = initMultiPartUploadResponse.UploadId;

            // Upload the parts to the S3 server.
            long partSize = 5 * Common.MiB;
            List<UploadPartResponse> uploadPartResponses = Common.UploadParts(
                s3Client,
                uploadTarget,
                filePath,
                uploadId,
                partSize);

            // Complete the upload to the S3 server.
            CompleteMultipartUploadResponse completeMultipartUploadResponse = Common.CloseUpload(
                s3Client,
                uploadTarget,
                filePath,
                uploadId,
                uploadPartResponses);
        }

        /// <summary>
        /// Given an upload target from the upload REST API, 
        /// compute a service URL for use with an S3 client.
        /// 
        /// Given uploadTaget = http[s]://{hostname}/Panopto/Upload/{guid}
        /// 
        /// Return http[s]://{hostname}/Panopto/
        /// </summary>
        /// <param name="uploadTarget">Upload target returned from the Upload REST API.</param>
        /// <returns>Service URL appropriate for use with an S3 client.</returns>
        private static string GetServiceUrlFromUploadTarget(
            string uploadTarget)
        {
            int i = uploadTarget.IndexOf(Common.UploadTargetPathFragment_Panopto);

            string result = uploadTarget.Substring(
                0,
                i + Common.UploadTargetPathFragment_Panopto.Length);

            return result;
        }

        /// <summary>
        /// Given an upload target from the upload REST API, 
        /// compute a content store file key prefix.
        /// 
        /// Given uploadTaget = http[s]://{hostname}/Panopto/Upload/{guid}
        /// 
        /// Return: {guid}
        /// </summary>
        /// <param name="uploadTarget">Upload target returned from the Upload REST API.</param>
        /// <returns>Content store file key prefix.</returns>
        private static string GetFileKeyPrefixFromUploadTarget(
            string uploadTarget)
        {
            int i = uploadTarget.IndexOf(Common.UploadTargetPathFragment_PanoptoUpload);

            string result = uploadTarget.Substring(
                i + Common.UploadTargetPathFragment_PanoptoUpload.Length,
                uploadTarget.Length - Common.UploadTargetPathFragment_PanoptoUpload.Length - i);

            return result;
        }

        /// <summary>
        /// Assemble a file key from an upload ID and a file name.
        /// 
        /// Given uploadTaget = http[s]://{hostname}/Panopto/Upload/{guid}
        /// and fileName = [{path}\]{fileName}
        /// 
        /// Return: {guid}/{fileName}
        /// </summary>
        /// <param name="uploadTarget">Upload target returned from the Upload REST API.</param>
        /// <param name="fileName">Local file name.</param>
        /// <returns>Content store file key prefix + file name</returns>
        private static string GetFileKey(string uploadTarget, string fileName)
        {
            return
                Path.Combine(
                    GetFileKeyPrefixFromUploadTarget(uploadTarget),
                    Path.GetFileName(fileName))
                    .Replace('\\', '/');
        }

        /// <summary>
        /// Helper function to create a REST object.
        /// </summary>
        /// <typeparam name="T">Class of object to create.</typeparam>
        /// <param name="authCookie">Auth cookie to pass to server.</param>
        /// <param name="noun">The noun used for this object type.</param>
        /// <param name="value">An instance of this object type from which to 
        /// create the server instance.</param>
        /// <param name="serverDns">The domain name for the server to connect to.</param>
        /// <returns>The newly-created object.</returns>
        public static T CreateRestObject<T>(
            string authCookie,
            string noun,
            T value,
            string serverDns) where T : class, new()
        {
            HttpWebRequest request = Common.CreateRequest(
                "POST",
                noun,
                authCookie,
                serverDns,
                Common.SerializeAsJson(value));

            return Common.GetResponse<T>(HttpStatusCode.Created, request);
        }

        /// <summary>
        /// Helper function to read a REST object.
        /// </summary>
        /// <typeparam name="T">Class of object to read.</typeparam>
        /// <param name="authCookie">Auth cookie to pass to server.</param>
        /// <param name="noun">The noun used for this object type.</param>
        /// <param name="objectID">ID of the object to read.</param>
        /// <param name="serverDns">The domain name for the server to connect to.</param>
        /// <returns>Object read from the server.</returns>
        public static T ReadRestObject<T>(
            string authCookie,
            string noun,
            Guid objectID,
            string serverDns) where T : class, new()
        {
            HttpWebRequest request = Common.CreateRequest(
                "GET",
                string.Format("{0}/{1}", noun, objectID),
                authCookie,
                serverDns);

            return Common.GetResponse<T>(HttpStatusCode.OK, request);
        }

        /// <summary>
        /// Helper function to update a REST object.
        /// </summary>
        /// <typeparam name="T">Class of object to update.</typeparam>
        /// <param name="authCookie">Auth cookie to pass to server.</param>
        /// <param name="noun">The noun used for this object type.</param>
        /// <param name="value">Instance of this object type containing
        /// the values to be saved on the server.</param>
        /// <param name="serverDns">The domain name for the server to connect to.</param>
        /// <returns>Updated object returned from the server.</returns>
        public static T UpdateRestObject<T>(
            string authCookie,
            string noun,
            Guid objectId,
            T value,
            string serverDns) where T : class, new()
        {
            HttpWebRequest request = Common.CreateRequest(
                "PUT",
                string.Format("{0}/{1}", noun, objectId),
                authCookie,
                serverDns,
                Common.SerializeAsJson(value));

            return Common.GetResponse<T>(HttpStatusCode.OK, request);
        }

        /// <summary>
        /// Helper function to delete a REST object.
        /// </summary>
        /// <typeparam name="T">Class of object to delete.</typeparam>
        /// <param name="authCookie">Auth cookie to pass to server.</param>
        /// <param name="noun">The noun used for this object type.</param>
        /// <param name="objectID">ID of the object to delete.</param>
        /// <param name="serverDns">The domain name for the server to connect to.</param>
        /// <returns>Object delete from the server.</returns>
        public static T DeleteRestObject<T>(
            string authCookie,
            string noun,
            Guid objectID,
            string serverDns) where T : class, new()
        {
            HttpWebRequest request = Common.CreateRequest(
                "DELETE",
                string.Format("{0}/{1}", noun, objectID),
                authCookie,
                serverDns);

            return Common.GetResponse<T>(HttpStatusCode.OK, request);
        }
    }

}
