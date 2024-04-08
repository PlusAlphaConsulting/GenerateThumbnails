// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security.Policy;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using GenerateThumbnails.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GenerateThumbnails
{
    public class GenerateThumbnails
    {
        // default parameter for generating thumbnail(s) if not provided in the body
        private const string DefaultParameterGenerateThumbnail = " -i {input} -vf thumbnail=n=100,scale=960:540 -frames:v 1 {tempFolder}\\Thumbnail%06d.jpg";

        private readonly ILogger _logger;

        public GenerateThumbnails(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GenerateThumbnails>();
        }



        /*
        Input :
        {
            "inputUrl":"https://mysasurlofthesourceblob",
            "outputUrl":"https://mysasurlofthedestinationcontainer",
            "ffmpegArguments" : " -i {input} -vf thumbnail=n=100,scale=960:540 -frames:v 1 {tempFolder}\\Thumbnail%06d.jpg"  // optional. This parameter generates 1 thumbnail from the first 100 frames in format 960x540
        }
        */

        /// <summary>
        /// This Azure function generates thumbnail(s) from a video file using ffmpeg.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [Function("GenerateThumbnails")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, ExecutionContext context)
        {
            bool isSuccessful = true;
            string errorText = string.Empty;
            int exitCode = 0;

            _logger.LogInformation("C# HTTP trigger function processed a request.");

            dynamic data;
            try
            {
                data = JsonConvert.DeserializeObject(await new StreamReader(req.Body).ReadToEndAsync());
            }
            catch (Exception ex)
            {
                return HelpersBasic.ReturnErrorException(_logger, ex);
            }

            var ffmpegArguments = (string)data.ffmpegArguments;

            var inputUrl = (string)data.inputUrl;
            if (inputUrl == null)
                return HelpersBasic.ReturnErrorException(_logger, "Error - please pass inputUrl in the JSON");

            var outputUrl = (string)data.outputUrl;
            if (outputUrl == null)
                return HelpersBasic.ReturnErrorException(_logger, "Error - please pass outputUrl in the JSON");

            _logger.LogInformation("Arguments : {0}", ffmpegArguments);

            try
            {
                var folder = Environment.GetEnvironmentVariable("HOME") + @"\site\wwwroot";
                var tempFolder = Path.Combine(Path.GetTempPath(), HelpersBasic.GenerateUniqueName("thumbnails"));

                // create temp folder
                Directory.CreateDirectory(tempFolder);

                var inputUri = new Uri(inputUrl);
                BlobUriBuilder inputBlobUriBuilder = new BlobUriBuilder(inputUri);

                string inputFileName = Path.GetFileName(inputUri.LocalPath);
                string pathLocalInput = Path.Combine(tempFolder, inputFileName);



                // GENERATE SAS FOR INPUT IF NEEDED
                if (!inputUrl.Contains("&sig="))
                {
                    // generate a SAS token for the input URL using managed entity
                    var storageAccountUriString = inputUri.Scheme + "://" + inputUri.Host;
                    // Create a DefaultAzureCredential to authenticate using managed identity
                    var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
                    // Create a BlobServiceClient
                    var blobServiceClient = new BlobServiceClient(new Uri(storageAccountUriString), credential);

                    var blobContainerClient = blobServiceClient.GetBlobContainerClient(inputBlobUriBuilder.BlobContainerName);
                    var blobClient = blobContainerClient.GetBlobClient(inputBlobUriBuilder.BlobName);

                    // Get a user delegation key for the Blob service that's valid for 2 hours.
                    var userDelegationKey = blobServiceClient.GetUserDelegationKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));

                    // Create a BlobSasBuilder
                    var sasBuilder = new BlobSasBuilder()
                    {
                        BlobContainerName = blobContainerClient.Name,
                        BlobName = blobClient.Name,
                        Resource = "b",
                        StartsOn = DateTimeOffset.UtcNow,
                        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                    };

                    sasBuilder.SetPermissions(BlobSasPermissions.Read); // Read permissions
                    inputBlobUriBuilder.Sas = sasBuilder.ToSasQueryParameters(userDelegationKey, blobServiceClient.AccountName);
                    inputUri = inputBlobUriBuilder.ToUri();
                    _logger.LogInformation($"SAS : {inputUri.ToString()}");
                }

                var outputUri = new Uri(outputUrl);
                BlobUriBuilder outputBlobUriBuilder = new BlobUriBuilder(outputUri);


                // GENERATE SAS FOR OUTPUT IF NEEDED
                if (!outputUrl.Contains("&sig="))
                {
                    // generate a SAS token for the input URL using managed entity
                    var storageAccountUriString = outputUri.Scheme + "://" + outputUri.Host;
                    // Create a DefaultAzureCredential to authenticate using managed identity
                    var credential = new DefaultAzureCredential(includeInteractiveCredentials: true);
                    // Create a BlobServiceClient
                    var blobServiceClient = new BlobServiceClient(new Uri(storageAccountUriString), credential);

                    var blobContainerClient = blobServiceClient.GetBlobContainerClient(outputBlobUriBuilder.BlobContainerName);
                    var blobClient = blobContainerClient.GetBlobClient("thumbnail.jpg");

                    // Get a user delegation key for the Blob service that's valid for 2 hours.
                    var userDelegationKey = blobServiceClient.GetUserDelegationKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));

                    // Create a BlobSasBuilder
                    var sasBuilder = new BlobSasBuilder()
                    {
                        BlobContainerName = blobContainerClient.Name,
                        BlobName = null,
                        Resource = "b",
                        StartsOn = DateTimeOffset.UtcNow,
                        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                    };

                    sasBuilder.SetPermissions(BlobSasPermissions.Write); // Write permissions
                    outputBlobUriBuilder.Sas = sasBuilder.ToSasQueryParameters(userDelegationKey, blobServiceClient.AccountName);
                    _logger.LogInformation($"Output SAS : {outputBlobUriBuilder.ToUri().ToString()}");
                    outputUri = outputBlobUriBuilder.ToUri();
                }


                foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    _logger.LogInformation($"{drive.Name}: {drive.TotalFreeSpace / 1024 / 1024} MB");
                }

                _logger.LogInformation("Generate thumbnail(s)...");

                // path to ffmpeg
                var local_root = Environment.GetEnvironmentVariable("AzureWebJobsScriptRoot");
                var azure_root = $"{Environment.GetEnvironmentVariable("HOME")}/site/wwwroot";
                var actual_root = local_root ?? azure_root;
                var fileFfmpeg = System.IO.Path.Combine(actual_root, "ffmpeg\\ffmpeg.exe");
                _logger.LogInformation("path to ffmpeg : " + fileFfmpeg);

                Process process = new Process();
                process.StartInfo.FileName = fileFfmpeg;

                process.StartInfo.Arguments = (ffmpegArguments ?? DefaultParameterGenerateThumbnail)
                    .Replace("{input}", "\"" + inputUri.ToString() + "\"")
                    .Replace("{tempFolder}", tempFolder)
                    .Replace("'", "\"");

                _logger.LogInformation(process.StartInfo.Arguments);

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.OutputDataReceived += new DataReceivedEventHandler(
                    (s, e) =>
                    {
                        _logger.LogInformation("O: " + e.Data);
                    }
                );
                process.ErrorDataReceived += new DataReceivedEventHandler(
                    (s, e) =>
                    {
                        _logger.LogInformation("E: " + e.Data);
                    }
                );

                //start ffmpeg process
                process.Start();
                _logger.LogInformation("ffmpeg process started.");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                exitCode = process.ExitCode;

                _logger.LogInformation("Thumbnail(s) generated.");

                // Create a BlobContainerClient from the SAS URL
                BlobContainerClient containerOutputClient = new BlobContainerClient(outputUri);

                // parse all files from the temp folder
                var filesThumbnails = Directory.GetFiles(tempFolder, "*.*");

                foreach (var fileThumbnail in filesThumbnails)
                {
                    _logger.LogInformation($"Uploading thumbnail to container... {fileThumbnail}");

                    // Replace with your blob name
                    string blobName = Path.GetFileName(fileThumbnail);

                    // Get a reference to the blob
                    BlobClient blobOutputClient = containerOutputClient.GetBlobClient(blobName);

                    try
                    {
                        using (FileStream fs = System.IO.File.OpenRead(fileThumbnail))
                        {
                            await blobOutputClient.UploadAsync(fs, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation("There was a problem uploading thumbnail file to blob. " + ex.ToString());
                        isSuccessful = false;
                        errorText += ex.Message;
                    }

                    _logger.LogInformation($"Thumbnail uploaded.                 {fileThumbnail}");

                    File.Delete(fileThumbnail);
                    _logger.LogInformation($"Thumbnail deleted from temp folder. {fileThumbnail}");
                }
                _logger.LogInformation("Thumbnail(s) uploaded.");

                // delete temp folder
                Directory.Delete(tempFolder);

            }
            catch (Exception e)
            {
                isSuccessful = false;
                errorText += e.Message;
            }

            if (exitCode != 0)
            {
                isSuccessful = false;
            }

            var response = new ProcessResult
            {
                IsSuccessful = isSuccessful,
                ErrorText = errorText
            };

            return new OkObjectResult(
                response
            );
        }
    }

    public class ProcessResult
    {
        public bool IsSuccessful { get; set; }
        public string? ErrorText { get; set; }
    }
}
