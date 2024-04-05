using System.Diagnostics;
using Azure.Storage.Blobs;
using GenerateThumbnails.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GenerateThumbnails
{
    public class GenerateThumbnails
    {
        private const string DefaultParameterGenerateThumbnail = " -i {input} -vf thumbnail=n=100,scale=960:540 -frames:v 1 {tempFolder}\\Thumbnail%06d.jpg";

        private readonly ILogger _logger;

        public GenerateThumbnails(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GenerateThumbnails>();
        }

        //
        // 
        //
        // ffmpeg -  This function generates a thumbnail with ffmpeg.
        //
        /*
        ```c#
        Input :
        {
            "inputUrl":"",
            "outputUrl":"",
            "ffmpegArguments" : " -i {input} -vf thumbnail=n=100,scale=960:540 -frames:v 1 {tempFolder}\\Thumbnail%06d.jpg"  // optional. This one generates 1 thumbnail from the first 100 frames in format 960x540
        }


        ```
        */
        [Function("GenerateThumbnails")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req, ExecutionContext context)
        {
            /*   _logger.LogInformation("C# HTTP trigger function processed a request.");

               var response = req.CreateResponse(HttpStatusCode.OK);
               response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

               response.WriteString("Welcome to Azure Functions!");

               return response;*/

            string output = string.Empty;
            bool isSuccessful = true;
            dynamic ffmpegResult = new JObject();
            string errorText = string.Empty;
            int exitCode = 0;

            _logger.LogInformation("C# HTTP trigger function processed a request.");

            dynamic data;
            try
            {
                data = JsonConvert.DeserializeObject(new StreamReader(req.Body).ReadToEnd());
            }
            catch (Exception ex)
            {
                return HelpersBasic.ReturnErrorException(_logger, ex);
            }

            var ffmpegArguments = (string)data.ffmpegArguments;

            var inputUrl = (string)data.sasInputUrl;
            if (inputUrl == null)
                return HelpersBasic.ReturnErrorException(_logger, "Error - please pass inputUrl in the JSON");

            var outputUrl = (string)data.sasOutputUrl;
            if (outputUrl == null)
                return HelpersBasic.ReturnErrorException(_logger, "Error - please pass outputUrl in the JSON");

            _logger.LogInformation("Arguments : {0}", ffmpegArguments);

            try
            {
                var folder = Environment.GetEnvironmentVariable("HOME") + @"\site\wwwroot";
                // var folder = context.FunctionDirectory;
                var tempFolder = Path.Combine(Path.GetTempPath(), HelpersBasic.GenerateUniqueName("thumbnails"));

                // create temp folder
                Directory.CreateDirectory(tempFolder);

                string inputFileName = Path.GetFileName(new Uri(inputUrl).LocalPath);
                string pathLocalInput = Path.Combine(tempFolder, inputFileName);

                string outputFileName = Path.GetFileName(new Uri(outputUrl).LocalPath);

                foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    _logger.LogInformation($"{drive.Name}: {drive.TotalFreeSpace / 1024 / 1024} MB");
                }

                _logger.LogInformation("Generate thumbnail(s)...");
                //var file = System.IO.Path.Combine(folder, "..\\ffmpeg\\ffmpeg.exe");
                var file = ".\\ffmpeg\\ffmpeg.exe";

                Process process = new Process();
                process.StartInfo.FileName = file;

                process.StartInfo.Arguments = (ffmpegArguments ?? DefaultParameterGenerateThumbnail)
                    .Replace("{input}", "\"" + inputUrl + "\"")
                    //.Replace("{tempFolder}", "\"" + tempFolder + "\"")
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
                //start process
                process.Start();
                _logger.LogInformation("ffmpeg process started.");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                exitCode = process.ExitCode;
                ffmpegResult = output;

                _logger.LogInformation("Thumbnail(s) generated.");

                var sasOutputUri = new Uri(outputUrl);

                // Create a BlobContainerClient from the SAS URL
                BlobContainerClient containerOutputClient = new BlobContainerClient(sasOutputUri);

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
                            blobOutputClient.Upload(fs, true);
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

            var response = new JObject
            {
                {"isSuccessful", isSuccessful},
                {"ffmpegResult",  ffmpegResult},
                {"errorText", errorText }

            };


            return new OkObjectResult(
                response
            );
        }
    }
}
