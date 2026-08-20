using FVMUtility.Models;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;

namespace FVUFileMove.Services
{
    public class SFTPProcessingService
    {
        private readonly string _apiUrl;

        public SFTPProcessingService(
            IConfiguration configuration)
        {
            _apiUrl =
                configuration["SFTPExecution:ApiUrl"]
                ?? throw new InvalidOperationException(
                    "SFTPExecution API URL is not configured.");
        }

        public void ProcessSFTP(
            BatchDetails batch,
            string zipFile)
        {
            Console.WriteLine();
            Console.WriteLine("SFTP Processing");
            Console.WriteLine("-------------------------");

            Console.WriteLine(
                "CSRBatchID: " + batch.CSRBatchID);

            Console.WriteLine(
                "ZIP Source: " + zipFile);

            // Check ZIP exists
            if (!File.Exists(zipFile))
            {
                throw new FileNotFoundException(
                    "ZIP file not found.",
                    zipFile);
            }

            Console.WriteLine(
                "Calling SFTPExecutionAPI...");

            using HttpClient client =
                new HttpClient()

                {
                    Timeout = TimeSpan.FromMinutes(30)
                };
            ;

          


            using MultipartFormDataContent content =
                new MultipartFormDataContent();

            using FileStream fileStream =
                File.OpenRead(zipFile);

            using StreamContent fileContent =
                new StreamContent(fileStream);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    "application/zip");

            // Add ZIP file
            content.Add(
                fileContent,
                "file",
                Path.GetFileName(zipFile));

            // Add CSRBatchID
            content.Add(
                new StringContent(
                    batch.CSRBatchID.ToString()),
                "batchId");

            Console.WriteLine(
                "Sending ZIP to Server B...");

            HttpResponseMessage response =
                client.PostAsync(
                    _apiUrl,
                    content)
                .GetAwaiter()
                .GetResult();

            string responseContent =
                response.Content
                    .ReadAsStringAsync()
                    .GetAwaiter()
                    .GetResult();

            Console.WriteLine(
                "Server B Response: "
                + responseContent);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    "SFTPExecutionAPI failed. "
                    + "Status: "
                    + response.StatusCode
                    + ", Response: "
                    + responseContent);
            }

            Console.WriteLine(
                "SFTP processing completed successfully.");
        }
    }
}



//using FVMUtility.Models;
//using Microsoft.Extensions.Configuration;
//using System.Diagnostics;

//namespace FVUFileMove.Services
//{
//    public class SFTPProcessingService
//    {
//        private readonly IConfiguration _configuration;
//        private readonly string _sftpInputPath;
//        private readonly string _sftpRunnerPath;

//        public SFTPProcessingService(
//            IConfiguration configuration)
//        {
//            _configuration = configuration;

//            _sftpInputPath =
//                _configuration["FileSettings:SFTPInputPath"]
//                ?? throw new InvalidOperationException(
//                    "SFTPInputPath is not configured.");

//            _sftpRunnerPath =
//                _configuration["FileSettings:SFTPRunnerPath"]
//                ?? throw new InvalidOperationException(
//                    "SFTPRunnerPath is not configured.");
//        }


//        public void ProcessSFTP(
//    BatchDetails batch,
//    string zipFile)
//        {
//            Console.WriteLine();
//            Console.WriteLine("SFTP Processing");
//            Console.WriteLine("-------------------------");

//            Console.WriteLine(
//                "CSRBatchID: " + batch.CSRBatchID);

//            Console.WriteLine(
//                "ZIP Source: " + zipFile);

//            // Check ZIP exists
//            if (!File.Exists(zipFile))
//            {
//                Console.WriteLine(
//                    "ZIP file not found: " + zipFile);

//                return;
//            }

//            // Create SFTP upload folder if required
//            if (!Directory.Exists(_sftpInputPath))
//            {
//                Directory.CreateDirectory(_sftpInputPath);
//            }

//            string fileName =
//                Path.GetFileName(zipFile);

//            string destination =
//                Path.Combine(
//                    _sftpInputPath,
//                    fileName);

//            Console.WriteLine(
//                "SFTP Destination: " + destination);

//            // Move ZIP to SFTP upload folder
//            File.Move(
//                zipFile,
//                destination);

//            Console.WriteLine(
//                "ZIP moved to SFTP upload folder.");

//            // Check SFTP Runner EXE
//            if (!File.Exists(_sftpRunnerPath))
//            {
//                Console.WriteLine(
//                    "SFTP Runner not found: "
//                    + _sftpRunnerPath);

//                return;
//            }

//            Console.WriteLine(
//                "SFTP Runner Path: "
//                + _sftpRunnerPath);

//            ProcessStartInfo processStartInfo =
//                new ProcessStartInfo
//                {
//                    FileName = _sftpRunnerPath,
//                    WorkingDirectory =
//                        Path.GetDirectoryName(
//                            _sftpRunnerPath),
//                    UseShellExecute = true
//                };

//            using Process? sftpProcess =
//                Process.Start(processStartInfo);

//            if (sftpProcess == null)
//            {
//                throw new Exception(
//                    "Unable to start SFTP Runner.");
//            }

//            Console.WriteLine(
//                "SFTP Runner started successfully.");

//            // await sftpProcess.WaitForExitAsync();
//            sftpProcess.WaitForExit();
//            Console.WriteLine(
//                "SFTP Runner process completed.");
//        }
//    }
//}