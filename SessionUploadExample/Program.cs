using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Amazon.S3;
using Amazon.S3.Model;

namespace SessionUploadExample
{
    class Program
    {
        /// <summary>
        /// Argument prefix used to specify server domain name from the command-line.
        /// </summary>
        public const string ServerDnsArgumentPrefix = "-serverDns";

        /// <summary>
        /// Argument prefix used to specify login username from the command-line.
        /// </summary>
        public const string UsernameArgumentPrefix = "-username";

        /// <summary>
        /// Argument prefix used to specify login password from the command-line.
        /// </summary>
        public const string PasswordArgumentPrefix = "-password";

        /// <summary>
        /// Argument prefix used to specify directory to crawl from the command-line.
        /// </summary>
        public const string DirectoryArgumentPrefix = "-directory";

        /// <summary>
        /// Argument prefix used to specify the Panopto folder id to upload to.
        /// </summary>
        public const string FolderIdArgumentPrefix = "-folderId";

        /// <summary>
        /// Argument prefix used to specify an output file with the results of the upload from the command-line.
        /// </summary>
        public const string OutputFilePathArgumentPrefix = "-output";

        /// <summary>
        /// Argument prefix used to specify that processing should continue even when an invalid XML file is encountered.
        /// </summary>
        public const string ContinueOnErrorArgumentPrefix = "-continueOnError";

        /// <summary>
        /// Argument prefix used to specify that SSL errors while connecting to a Panopto server should be ignored.
        /// </summary>
        public const string IgnoreSSLErrorsArgumentPrefix = "-ignoreSSLErrors";

        /// <summary>
        /// Argument used to print usage.
        /// </summary>
        public const string HelpArgument1 = "-?";

        /// <summary>
        /// Argument used to print usage.
        /// </summary>
        public const string HelpArgument2 = "-help";

        /// <summary>
        /// A collection of terminating states for an upload.
        /// </summary>
        public static readonly UploadStatus[] CompletedStates = new UploadStatus[]
        {
            UploadStatus.Complete,
            UploadStatus.Deleted,
            UploadStatus.DeletingError,
            UploadStatus.ProcessingError,
            UploadStatus.UploadCancelled
        };

        private static void Main(string[] args)
        {
            if (args.Any(arg => arg == Program.HelpArgument1 || arg == Program.HelpArgument2))
            {
                Program.PrintUsageAndExit();
            }

            string authCookie = null;
            string serverDns = null;
            string directory = null;
            Guid folderId;
            string outputFilePath = null;
            bool continueOnError = false;
            bool ignoreSSLErrors = false;

            Program.GetArguments(
                args,
                out authCookie,
                out serverDns,
                out directory,
                out folderId,
                out outputFilePath,
                out continueOnError,
                out ignoreSSLErrors);

            Common.IgnoreSSLErrors = ignoreSSLErrors;

            Dictionary<string, SessionUpload> uploads = Program.StartUploads(
                authCookie, 
                serverDns, 
                directory, 
                folderId, 
                continueOnError);

            Console.WriteLine("All uploads submitted. Polling for status until all have finished processing.");

            // Check status of uploads
            while (uploads.Values.Any(upload => !Program.CompletedStates.Contains(upload.State)))
            {
                Console.WriteLine();

                int i = 0;
                foreach (string key in uploads.Keys.ToArray())
                {
                    uploads[key] = Common.ReadRestObject<SessionUpload>(
                        authCookie,
                        "sessionUpload",
                        uploads[key].ID,
                        serverDns);

                    Console.WriteLine(
                        "Upload {0} ({1}) processing state is {2}{3}.", 
                        i,
                        Path.GetFileName(key),
                        uploads[key].State,
                        (uploads[key].SessionId.HasValue ? (" (session id = " + uploads[key].SessionId.Value + ")") : string.Empty));

                    if (!string.IsNullOrEmpty(uploads[key].Message))
                    {
                        Console.WriteLine(
                            "Upload {0} processing message is {1}.",
                            i,
                            uploads[key].Message);
                    }

                    i++;
                }

                Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            Console.WriteLine("All uploads finished.");

            // Output results to designated file
            if (!string.IsNullOrEmpty(outputFilePath))
            {
                Console.WriteLine("Saving results to {0}", outputFilePath);

                DataContractSerializer serializer = new DataContractSerializer(typeof(Tuple<string, SessionUpload>[]));
                using (FileStream outputFileStream = File.Open(outputFilePath, FileMode.Create))
                {
                    serializer.WriteObject(
                        outputFileStream,
                        uploads
                            .Select(upload => Tuple.Create(Path.GetFileName(upload.Key), upload.Value))
                            .ToArray());
                }
            }

            Console.WriteLine("Exiting.");
        }

        /// <summary>
        /// Upload files in given directory to designated folder on designated server
        /// </summary>
        /// <param name="authCookie">authentication cookie</param>
        /// <param name="serverDns">destination server</param>
        /// <param name="directory">directory containing files to upload</param>
        /// <param name="folderId">destination folder on server</param>
        /// <param name="continueOnError">proceed to next upload if an error exists</param>
        /// <returns>list SessionUpload object</returns>
        private static Dictionary<string, SessionUpload> StartUploads(
            string authCookie,
            string serverDns,
            string directory,
            Guid folderId,
            bool continueOnError)
        {
            Dictionary<string, SessionUpload> uploads = new Dictionary<string, SessionUpload>();

            // collect all XML files in the folder
            string[] xmlFilePaths = Directory.EnumerateFiles(directory, "*.xml", SearchOption.AllDirectories).ToArray();

            // errors encountered while deserializing XML files as PanoptoSessions
            Dictionary<string, Exception> deserializationExceptions = new Dictionary<string,Exception>();

            // a collection of all the PanoptoSession XML files found and the list of files that session references
            Dictionary<string, string[]> referencedFilesByManifest = new Dictionary<string, string[]>();

            // try to parse each as a Panopto session
            foreach (string xmlFilePath in xmlFilePaths)
            {
                PanoptoSession panoptoSession = null;
                
                using (FileStream xmlFileStream = File.Open(xmlFilePath, FileMode.Open))
                {
                    try
                    {
                        XmlSerializer deserializer = new XmlSerializer(typeof(PanoptoSession));
                        panoptoSession = (PanoptoSession)deserializer.Deserialize(xmlFileStream);
                    }
                    catch (Exception e)
                    {
                        // ignore the error for now, it may just be a referenced XML file
                        deserializationExceptions[xmlFilePath] = e;
                        continue;
                    }
                }

                // collect filenames in the session to see if they exist
                Dictionary<string, PanoptoSessionPresentation> presentations = (panoptoSession.Presentations != null)
                    ? panoptoSession.Presentations.ToDictionary(presentation => presentation.Filename.Value)
                    : new Dictionary<string, PanoptoSessionPresentation>();

                Dictionary<string, PanoptoSessionVideo> streams = (panoptoSession.Videos != null)
                    ? panoptoSession.Videos.ToDictionary(stream => stream.Filename.Value)
                    : new Dictionary<string, PanoptoSessionVideo>();

                // create a list of all requisite filenames
                string[] filesReferencedInSession = presentations.Keys
                    .Concat(streams.Keys)
                    .ToArray();

                // record the manifest and related files
                referencedFilesByManifest[xmlFilePath] = filesReferencedInSession;
            }

            // remove any XML files that couldn't be parsed but that are referenced by a PanoptoSession
            foreach (string[] referencedFiles in referencedFilesByManifest.Values)
            {
                foreach (string referencedFile in referencedFiles)
                {
                    deserializationExceptions.Remove(referencedFile);
                }
            }

            // make sure there weren't any XML files that are unaccounted for
            if (deserializationExceptions.Any())
            {
                foreach (KeyValuePair<string, Exception> deserializationException in deserializationExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error deserializing XML file {0} as a PanoptoSession", deserializationException.Key);
                    Console.WriteLine(deserializationException.Value.ToString());
                }

                if (!continueOnError)
                {
                    Console.WriteLine("Please fix or remove the invalid XML files before continuing");
                    Environment.Exit(0);
                }
                else
                {
                    Console.WriteLine("ContinueOnError is true, continuing with uploads");
                }
            }

            foreach (KeyValuePair<string, string[]> manifestWithFiles in referencedFilesByManifest)
            {
                uploads[manifestWithFiles.Key] = Program.StartNewUpload(
                    manifestWithFiles.Key,
                    manifestWithFiles.Value,
                    authCookie,
                    serverDns,
                    folderId,
                    continueOnError);
            }

            return uploads;
        }

        /// <summary>
        /// Start a new upload for a session
        /// </summary>
        /// <param name="manifestPath">location of manifest file</param>
        /// <param name="referencedFiles">Files referenced in the manifest file</param>
        /// <param name="authCookie">authentication cookie</param>
        /// <param name="serverDns">destination server</param>
        /// <param name="folderId">destination folder</param>
        /// <param name="continueOnError">continue to next file in case of error</param>
        /// <returns>SessionUpload object</returns>
        private static SessionUpload StartNewUpload(
            string manifestPath,
            IEnumerable<string> referencedFiles,
            string authCookie,
            string serverDns,
            Guid folderId,
            bool continueOnError)
        {
            Console.WriteLine("\nUploading {0} and files", manifestPath);

            SessionUpload upload = Common.CreateRestObject<SessionUpload>(
                authCookie,
                "sessionUpload",
                new SessionUpload()
                {
                    FolderId = folderId
                },
                serverDns);

            Guid uploadPublicID = upload.ID;
            string uploadTarget = upload.UploadTarget;

            Console.WriteLine("Created upload bucket {0} at {1}. Uploading files now.", uploadPublicID, uploadTarget);

            // the files will all be rooted relative to the manifest, so get the directory to use for finding files
            FileInfo fileInfo = new FileInfo(manifestPath);
            string directory = fileInfo.Directory.FullName;

            string[] filePathsToUpload = (new string[] { manifestPath })
                .Concat(referencedFiles.Select(file => Path.Combine(directory, file)))
                .ToArray();

            foreach (string filePath in filePathsToUpload)
            {
                Console.Write("Uploading {0} ... ", (new FileInfo(filePath)).Name);

                if (File.Exists(filePath))
                {
                    Common.TranslocateFile(uploadTarget, filePath);
                    Console.WriteLine("finished!");
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("File does not exist: {0}", filePath);

                    if (continueOnError)
                    {
                        Console.WriteLine("ContinueOnError is true, skipping file and continuing with uploads");
                    }
                    else
                    {
                        Console.WriteLine("Please fix or remove the invalid file reference before continuing");
                        Environment.Exit(0);
                    }
                }
            }

            Console.WriteLine("All files finished. Telling server to process files now.");

            upload.State = UploadStatus.UploadComplete;

            upload = Common.UpdateRestObject<SessionUpload>(
                authCookie,
                "sessionUpload",
                uploadPublicID,
                upload,
                serverDns);

            return upload;
        }

        /// <summary>
        /// Parse input arguments
        /// </summary>
        /// <param name="args">original arguments</param>
        /// <param name="authCookie">authentication cookie</param>
        /// <param name="serverDns">destination server</param>
        /// <param name="directory">upload directory</param>
        /// <param name="folderId">destination folder</param>
        /// <param name="outputFilePath">file to write results into</param>
        /// <param name="continueOnError">whether to continue in case of error</param>
        /// <param name="ignoreSSLErrors">whether to ignore SSL errors</param>
        private static void GetArguments(
            string[] args,
            out string authCookie,
            out string serverDns,
            out string directory,
            out Guid folderId,
            out string outputFilePath,
            out bool continueOnError,
            out bool ignoreSSLErrors)
        {
            authCookie = null;

            // first try to get arg values from the command-line
            serverDns = Program.GetValueFromArgs(Program.ServerDnsArgumentPrefix, args);
            string username = Program.GetValueFromArgs(Program.UsernameArgumentPrefix, args);
            string password = Program.GetValueFromArgs(Program.PasswordArgumentPrefix, args);
            directory = Program.GetValueFromArgs(Program.DirectoryArgumentPrefix, args);
            string folderIdString = Program.GetValueFromArgs(Program.FolderIdArgumentPrefix, args);
            outputFilePath = Program.GetValueFromArgs(Program.OutputFilePathArgumentPrefix, args);
            string continueOnErrorString = Program.GetValueFromArgs(Program.ContinueOnErrorArgumentPrefix, args);
            string ignoreSSLErrorsString = Program.GetValueFromArgs(Program.IgnoreSSLErrorsArgumentPrefix, args);

            if (!string.IsNullOrEmpty(continueOnErrorString))
            {
                continueOnError = bool.Parse(continueOnErrorString);
            }
            else
            {
                continueOnError = false;
            }

            if (!string.IsNullOrEmpty(ignoreSSLErrorsString))
            {
                ignoreSSLErrors = bool.Parse(ignoreSSLErrorsString);
            }
            else
            {
                ignoreSSLErrors = false;
            }

            Common.IgnoreSSLErrors = ignoreSSLErrors;

            while (string.IsNullOrEmpty(serverDns))
            {
                Console.WriteLine("Panopto server domain name? (no protocols or slashes, i.e. - demo.hosted.panopto.com)");
                serverDns = Console.ReadLine();
            }

            while (string.IsNullOrEmpty(username))
            {
                Console.WriteLine("Panopto login username?");
                username = Console.ReadLine();
            }

            while (string.IsNullOrEmpty(password))
            {
                Console.WriteLine("Panopto login password?");

                ConsoleKeyInfo consoleKeyInfo;
                while (true)
                {
                    consoleKeyInfo = Console.ReadKey(true);
                    if (consoleKeyInfo.Key == ConsoleKey.Enter)
                    {
                        break;
                    }

                    password += consoleKeyInfo.KeyChar.ToString();
                }
            }

            authCookie = Common.LogonAndGetCookie(username, password, serverDns);

            if (string.IsNullOrEmpty(authCookie))
            {
                Console.WriteLine("Invalid credentials for user {0} on server {1}.", username, serverDns);
                Environment.Exit(0);
            }

            while (string.IsNullOrEmpty(directory))
            {
                Console.WriteLine("Directory with XML manifest and media files to upload?");
                directory = Console.ReadLine();
            }

            if (!Directory.Exists(directory))
            {
                Console.WriteLine("Specified directory {0} does not exist.", directory);
                Environment.Exit(0);
            }

            while (string.IsNullOrEmpty(folderIdString))
            {
                Console.WriteLine("Panopto folder id to upload new sessions to?");
                folderIdString = Console.ReadLine();
            }

            folderId = Guid.Parse(folderIdString);            
        }

        /// <summary>
        /// Obtain values for specified option with given prefix
        /// </summary>
        /// <param name="prefix">prefix for option</param>
        /// <param name="args">arguments to look in</param>
        /// <returns>argument value for specified option</returns>
        private static string GetValueFromArgs(string prefix, string[] args)
        {
            string nameValuePair = args.FirstOrDefault(arg => arg.StartsWith(prefix));
            string value = null;

            if (nameValuePair != null)
            {
                string[] parts = nameValuePair.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    value = parts[1];
                }
            }

            return value;
        }

        /// <summary>
        /// Print usage and exit program
        /// </summary>
        private static void PrintUsageAndExit()
        {
            Console.WriteLine();
            Console.WriteLine(
                "SessionUploadExample.exe {0}:<server dns> {1}:<username> {2}:<password> {3}:<directory> {4}:<folder id> {5}:<output file> {6}:<continue on error> {7}:<ignore SSL errors>",
                Program.ServerDnsArgumentPrefix,
                Program.UsernameArgumentPrefix,
                Program.PasswordArgumentPrefix,
                Program.DirectoryArgumentPrefix,
                Program.FolderIdArgumentPrefix,
                Program.OutputFilePathArgumentPrefix,
                Program.ContinueOnErrorArgumentPrefix,
                Program.IgnoreSSLErrorsArgumentPrefix);
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("<server dns> - Panopto server domain name (no protocols or slashes, i.e. - demo.hosted.panopto.com)");
            Console.WriteLine("<username> - Panopto login username");
            Console.WriteLine("<password> - Panopto login password");
            Console.WriteLine("<directory> - local path to files to upload");
            Console.WriteLine("<folder id> - Panopto folder id of the folder to upload to");
            Console.WriteLine("<output file> - optional - name of file to output upload results to");
            Console.WriteLine("<continue on error> - optional (default false) - specifies if the upload should continue even if a problem is detected with the upload");
            Console.WriteLine("<ignore SSL errors> - optional (default false) - specifies if SSL errors should be ignored while connecting a Panopto server");

            Console.WriteLine("All arguments are optional and will be read from the interactive command-line if not specified.");
            Environment.Exit(0);
        }
    }
}
