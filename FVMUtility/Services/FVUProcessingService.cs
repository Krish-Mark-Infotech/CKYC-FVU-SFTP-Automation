using FVMUtility.Models;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace FVUFileMove.Services
{
    public class FVUProcessingService
    {
        private readonly IConfiguration _configuration;
        private readonly DatabaseService _databaseService;
        private readonly SFTPProcessingService _sftpProcessingService;


        private readonly string _fvuInputPath;
        private readonly string _fvuUtilityPath;
        private readonly string _fvuOutputPath;
        private readonly string _errorPath;
        private readonly string _successPath;
        private readonly bool _isSftpUploadEnabled;
       


        public FVUProcessingService(
            IConfiguration configuration)
        {
            _configuration = configuration;

            _databaseService =
                new DatabaseService(configuration);

            _sftpProcessingService =
            new SFTPProcessingService(configuration);
           
            _fvuInputPath =
                   _configuration["FileSettings:FVUInputPath"]
                   ?? throw new InvalidOperationException(
                       "FVUInputPath is not configured.");
            _fvuUtilityPath =
                    _configuration["FileSettings:FVUUtilityPath"]
                    ?? throw new InvalidOperationException(
                        "FVUUtilityPath is not configured.");

            _fvuOutputPath =
                    _configuration["FileSettings:FVUOutputPath"]
                    ?? throw new InvalidOperationException(
                        "FVUOutputPath is not configured.");

            _errorPath =
                    _configuration["FileSettings:ErrorPath"]
                    ?? throw new InvalidOperationException(
                        "ErrorPath is not configured.");

            _successPath =
                    _configuration["FileSettings:SuccessPath"]
                    ?? throw new InvalidOperationException(
                        "SuccessPath is not configured.");

            _isSftpUploadEnabled =
                string.Equals(
                    _configuration["ProcessingSettings:EnableSFTPUpload"],
                    "true",
                    StringComparison.OrdinalIgnoreCase);



        }


        public void ProcessMWBatch(string mwBatchId)
        {
            if (string.IsNullOrWhiteSpace(mwBatchId))
            {
                Console.WriteLine("MWBatchID is required.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("MW Batch ID: " + mwBatchId);
            Console.WriteLine("-------------------------");

            // Get all pending CSR batches under the given MWBatchID
            List<BatchDetails> pendingBatches =
                _databaseService.GetPendingBatches(mwBatchId);

            if (pendingBatches.Count == 0)
            {
                Console.WriteLine(
                    "No pending batches found for MWBatchID: "
                    + mwBatchId);

                return;
            }

            Console.WriteLine(
                "Pending CSR Batches Found: "
                + pendingBatches.Count);

            // Change P ? VI for all pending batches
            foreach (BatchDetails batch in pendingBatches)
            {
                _databaseService.UpdateBatchStatus(
                    batch.CSRBatchID,
                    "VI");

                Console.WriteLine(
                    "CSRBatchID "
                    + batch.CSRBatchID
                    + " : P ? VI");
            }

            Console.WriteLine();
            Console.WriteLine(
                "All pending batches have been marked as VI.");

            // FVU utility uses shared input/output folders, so process each CSR batch in sequence.
            foreach (BatchDetails batch in pendingBatches)
            {
                 ProcessPendingBatch(batch);
            }
            Console.WriteLine();
            Console.WriteLine(
                "All CSR batches processed.");
        }

        private void  ProcessPendingBatch(BatchDetails batch)
        {
            Console.WriteLine(
                "Processing CSRBatchID: " +
                batch.CSRBatchID);

            try
            {
                string csrFilePath =
                    _databaseService.GetCSRFilePath(
                        batch.TransactionType);

                if (string.IsNullOrWhiteSpace(csrFilePath))
                {
                    Console.WriteLine(
                        "CSR file path not configured for CSRBatchID: "
                        + batch.CSRBatchID);

                    _databaseService.UpdateBatchStatus(
                        batch.CSRBatchID,
                        "VF",
                        "File not found");

                    return;
                }

                string batchFolderPath =
                    Path.Combine(
                        csrFilePath,
                        batch.CSRBatchID.ToString(),
                        "File");

                Console.WriteLine(
                    "Checking folder: " +
                    batchFolderPath);

                if (!Directory.Exists(batchFolderPath))
                {
                    Console.WriteLine(
                        "File not found: " +
                        batchFolderPath);

                    _databaseService.UpdateBatchStatus(
                        batch.CSRBatchID,
                        "VF",
                        "File not found");

                    return;
                }

                // Get expected file extension based on TransactionType
                string expectedExtension =
                    GetExpectedFileExtension(
                        batch.TransactionType);

                if (string.IsNullOrWhiteSpace(expectedExtension))
                {
                    Console.WriteLine(
                        "Unsupported TransactionType: "
                        + batch.TransactionType);

                    _databaseService.UpdateBatchStatus(
                        batch.CSRBatchID,
                        "VF",
                        "Unsupported TransactionType");

                    return;
                }

                Console.WriteLine(
                    "Transaction Type: "
                    + batch.TransactionType);

                Console.WriteLine(
                    "Expected File Type: "
                    + expectedExtension);

                // Find the required file type
                string[] files =
                    Directory.GetFiles(
                        batchFolderPath,
                        "*" + expectedExtension,
                        SearchOption.TopDirectoryOnly);

                if (files.Length == 0)
                {
                    Console.WriteLine(
                        expectedExtension
                        + " file not found for CSRBatchID: "
                        + batch.CSRBatchID);

                    _databaseService.UpdateBatchStatus(
                        batch.CSRBatchID,
                        "VF",
                        "File not found");

                    return;
                }

                Console.WriteLine(
                    expectedExtension
                    + " files found: "
                    + files.Length);

                List<string> movedFiles = new List<string>();

                foreach (string file in files)
                {
                    string fileName =
                        Path.GetFileName(file);

                    string destinationFile =
                        Path.Combine(
                            _fvuInputPath,
                            fileName);

                    Console.WriteLine(
                        "Source      : " + file);

                    Console.WriteLine(
                        "Destination : " + destinationFile);

                    if (File.Exists(destinationFile))
                    {
                        File.Delete(destinationFile);
                    }

                    File.Copy(
                        file,
                        destinationFile,
                        true);

                    Console.WriteLine(
                        "File copied successfully.");

                    movedFiles.Add(destinationFile);
                }


                string sourceDocFolderPath =
                    Path.Combine(
                        csrFilePath,
                        batch.CSRBatchID.ToString(),
                        "DOC");

                string fvuSupportDocsPath =
                    Path.Combine(
                        Path.GetDirectoryName(_fvuUtilityPath) ?? string.Empty,
                        "support_docs");

                CleanWorkingFolder(
                    fvuSupportDocsPath,
                    "FVU support docs");

                CopySupportDocuments(
                    sourceDocFolderPath,
                    fvuSupportDocsPath);
                // Next:
                // Copy files to FVU input
                // Copy docs to FVU support_docs
                // Run FVU utility
                // Handle TXT / ZIP

                Console.WriteLine();
                Console.WriteLine("FVU Utility");
                Console.WriteLine("-------------------------");

                Console.WriteLine(
                    "FVU Utility Path: " +
                    _fvuUtilityPath);

                if (!File.Exists(_fvuUtilityPath))
                {
                    Console.WriteLine(
                        "FVU Utility not found.");

                    _databaseService.UpdateBatchStatus(
                        batch.CSRBatchID,
                        "VF",
                        "FVU Utility not found");

                    return;
                }

                ProcessStartInfo processStartInfo =
                    new ProcessStartInfo
                    {
                        FileName = _fvuUtilityPath,
                        WorkingDirectory =
                            Path.GetDirectoryName(_fvuUtilityPath),
                        UseShellExecute = true
                    };

                //executes the exe file  
                using Process? fvuProcess =
                    Process.Start(processStartInfo);

                if (fvuProcess == null)
                {
                    throw new Exception(
                        "Unable to start FVU Utility.");
                }

                Console.WriteLine(
                    "FVU Utility started successfully.");

                // fvuProcess.WaitForExitAsync();
                fvuProcess.WaitForExit();

                Console.WriteLine(
                    "FVU Utility process completed.");

                // CLEAN FVU INPUT FOLDER
                // =========================

                Console.WriteLine();
                Console.WriteLine("Cleaning FVU Input folder...");

                CleanWorkingFolder(
                    _fvuInputPath,
                    "FVU input");

                CleanWorkingFolder(
                    Path.Combine(
                        Path.GetDirectoryName(_fvuUtilityPath) ?? string.Empty,
                        "support_docs"),
                    "FVU support docs");

                 ProcessFVUOutput(
                    batch,
                    movedFiles,
                    csrFilePath);
                //await ProcessFVUOutput(
                //                    batch,
                //                    files);
            }

            catch (Exception ex)
            {
                Console.WriteLine(
                    "Error processing CSRBatchID "
                    + batch.CSRBatchID
                    + ": "
                    + ex.Message);

                _databaseService.UpdateBatchStatus(
                    batch.CSRBatchID,
                    "VF",
                    ex.Message);
            }

            //await Task.CompletedTask;
        }

        private static void CopySupportDocuments(
            string sourceDocFolderPath,
            string fvuSupportDocsPath)
        {
            if (string.IsNullOrWhiteSpace(sourceDocFolderPath)
                || !Directory.Exists(sourceDocFolderPath))
            {
                Console.WriteLine(
                    "Source DOC folder not found. Skipping support docs copy: "
                    + sourceDocFolderPath);

                return;
            }

            Directory.CreateDirectory(fvuSupportDocsPath);

            string[] sourceFiles =
                Directory.GetFiles(
                    sourceDocFolderPath,
                    "*",
                    SearchOption.AllDirectories);

            Console.WriteLine(
                "Support docs found: "
                + sourceFiles.Length);

            foreach (string sourceFile in sourceFiles)
            {
                string relativePath =
                    sourceFile.Substring(sourceDocFolderPath.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                string destinationFile =
                    Path.Combine(
                        fvuSupportDocsPath,
                        relativePath);

                string destinationDirectory =
                    Path.GetDirectoryName(destinationFile) ?? fvuSupportDocsPath;

                Directory.CreateDirectory(destinationDirectory);

                File.Copy(
                    sourceFile,
                    destinationFile,
                    true);

                Console.WriteLine(
                    "Support doc copied: "
                    + sourceFile
                    + " -> "
                    + destinationFile);
            }
        }

        private static void CleanWorkingFolder(
            string folderPath,
            string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            Directory.CreateDirectory(folderPath);

            string[] files =
                Directory.GetFiles(
                    folderPath,
                    "*",
                    SearchOption.AllDirectories);

            foreach (string file in files)
            {
                try
                {
                    File.Delete(file);

                    Console.WriteLine(
                        "Deleted "
                        + folderName
                        + " working file: "
                        + file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        "Unable to delete "
                        + folderName
                        + " working file: "
                        + file
                        + " - "
                        + ex.Message);
                }
            }

            Console.WriteLine(
                folderName
                + " cleanup completed.");
        }
        private string GetExpectedFileExtension(string transactionType)
        {
            return transactionType.Trim().ToUpperInvariant() switch
            {
                "SEARCH" => ".SRC",
                "DOWNLOAD" => ".DWN",
                "REGISTRATION" => ".UPL",
                _ => string.Empty
            };
        }


        private void ProcessFVUOutput(
                            BatchDetails batch,
                            List<string> movedFiles,
                            string csrFilePath)

        {
            Console.WriteLine();
            Console.WriteLine("Checking FVU output...");
            Console.WriteLine(
                "FVU Output Path: " + _fvuOutputPath);

            if (!Directory.Exists(_fvuOutputPath))
            {
                _databaseService.UpdateBatchStatus(
                    batch.CSRBatchID,
                    "VF",
                    "FVU output folder not found");

                return;
            }

            foreach (string inputFile in movedFiles)
            {
                string inputFileName =
                    Path.GetFileNameWithoutExtension(inputFile);

                Console.WriteLine();
                Console.WriteLine(
                    "Looking for FVU output for: "
                    + inputFileName);

                string[] outputFiles =
                    Directory.GetFiles(
                        _fvuOutputPath,
                        inputFileName + ".*",
                        SearchOption.TopDirectoryOnly);

                string? txtFile =
                    outputFiles.FirstOrDefault(
                        f => string.Equals(
                            Path.GetExtension(f),
                            ".ERR",
                            StringComparison.OrdinalIgnoreCase));

                string? zipFile =
                    outputFiles.FirstOrDefault(
                        f => string.Equals(
                            Path.GetExtension(f),
                            ".zip",
                            StringComparison.OrdinalIgnoreCase));

                // =========================
                // TXT = ERROR
                // =========================

                if (txtFile != null)
                {
                    string batchErrorPath =
                        Path.Combine(
                            csrFilePath,
                            batch.CSRBatchID.ToString());
                        //,   "Error"); commented for taking error file in same folder as input file


                    Directory.CreateDirectory(
                        batchErrorPath);

                    string destination =
                        Path.Combine(
                            batchErrorPath,
                            Path.GetFileName(txtFile));

                    Console.WriteLine(
                        "TXT found: " + txtFile);

                    Console.WriteLine(
                        "Moving TXT to: " + destination);

                    File.Move(
                        txtFile,
                        destination);

                    Console.WriteLine(
                        "TXT moved successfully.");

                    string fvuOutputFileName =
                            Path.GetFileName(destination);
                    //Console.WriteLine(destination);
                    //Console.WriteLine(fvuOutputFileName);

                    _databaseService.UpdateBatchStatus(
                        batch.CSRBatchID,
                        "VF",
                        destination,
                        fvuOutputFileName);
                       // destination);

                    return;
                }

                // =========================
                // ZIP = SUCCESS
                // =========================
                if (zipFile != null)
                {
                    string batchSuccessPath =
                        Path.Combine(
                            csrFilePath,
                            batch.CSRBatchID.ToString());

                    Directory.CreateDirectory(
                        batchSuccessPath);

                    string destination =
                        Path.Combine(
                            batchSuccessPath,
                            Path.GetFileName(zipFile));

                    Console.WriteLine(
                        "ZIP found: " + zipFile);

                    Console.WriteLine(
                        "Moving ZIP to: " + destination);

                    File.Move(
                        zipFile,
                        destination);

                    Console.WriteLine(
                        "ZIP moved successfully.");
                    //success file path save to db 
                    //_databaseService.UpdateBatchStatus(
                    //    batch.CSRBatchID,
                    //    "VS",
                    //    destination);


                    string fvuOutputFileName =
                        Path.GetFileName(destination);

                    _databaseService.UpdateBatchStatus(
                        batch.CSRBatchID,
                        "VS",
                        null,
                        fvuOutputFileName);

                    if (_isSftpUploadEnabled)
                    {
                        // Start SFTP processing
                         _sftpProcessingService.ProcessSFTP(
                            batch,
                            destination);
                    }
                    else
                    {
                        Console.WriteLine(
                            "SFTP upload is disabled by configuration.");
                    }

                    return;
                }
                // =========================
                // NOTHING FOUND
                // =========================

                Console.WriteLine(
                    "No matching TXT or ZIP found for: "
                    + inputFileName);

                _databaseService.UpdateBatchStatus(
                    batch.CSRBatchID,
                    "VF",
                    "No matching FVU output found.");
            }

            //await Task.CompletedTask;
        }

        //    private async Task ProcessFVUOutput(
        //BatchDetails batch,
        //string[] inputFiles)
        //    {
        //        Console.WriteLine();
        //        Console.WriteLine("Checking FVU output...");
        //        Console.WriteLine(
        //            "FVU Output Path: " + _fvuOutputPath);

        //        if (!Directory.Exists(_fvuOutputPath))
        //        {
        //            _databaseService.UpdateBatchStatus(
        //                batch.CSRBatchID,
        //                "VF",
        //                "FVU output folder not found");

        //            return;
        //        }

        //        string[] txtFiles =
        //            Directory.GetFiles(
        //                _fvuOutputPath,
        //                "*.txt",
        //                SearchOption.TopDirectoryOnly);

        //        string[] zipFiles =
        //            Directory.GetFiles(
        //                _fvuOutputPath,
        //                "*.zip",
        //                SearchOption.TopDirectoryOnly);

        //        if (txtFiles.Length > 0)
        //        {
        //            Console.WriteLine(
        //                "TXT output found: "
        //                + txtFiles.Length);

        //            Directory.CreateDirectory(_errorPath);

        //            foreach (string file in txtFiles)
        //            {
        //                string destination =
        //                    Path.Combine(
        //                        _errorPath,
        //                        Path.GetFileName(file));

        //                Console.WriteLine(
        //                    "Moving TXT:");

        //                Console.WriteLine(
        //                    "Source      : " + file);

        //                Console.WriteLine(
        //                    "Destination : " + destination);

        //                File.Move(
        //                    file,
        //                    destination);

        //                Console.WriteLine(
        //                    "TXT moved successfully.");
        //            }

        //            _databaseService.UpdateBatchStatus(
        //                batch.CSRBatchID,
        //                "VF",
        //                "FVU validation failed. TXT output generated.");

        //            return;
        //        }

        //        if (zipFiles.Length > 0)
        //        {
        //            Console.WriteLine(
        //                "ZIP output found: "
        //                + zipFiles.Length);

        //            Directory.CreateDirectory(_successPath);

        //            foreach (string file in zipFiles)
        //            {
        //                string destination =
        //                    Path.Combine(
        //                        _successPath,
        //                        Path.GetFileName(file));

        //                Console.WriteLine(
        //                    "Moving ZIP:");

        //                Console.WriteLine(
        //                    "Source      : " + file);

        //                Console.WriteLine(
        //                    "Destination : " + destination);

        //                File.Move(
        //                    file,
        //                    destination);

        //                Console.WriteLine(
        //                    "ZIP moved successfully.");
        //            }

        //            _databaseService.UpdateBatchStatus(
        //                batch.CSRBatchID,
        //                "VS",
        //                null);

        //            return;
        //        }

        //        Console.WriteLine(
        //            "No TXT or ZIP output found.");

        //        _databaseService.UpdateBatchStatus(
        //            batch.CSRBatchID,
        //            "VF",
        //            "No FVU output file found.");

        //        await Task.CompletedTask;
        //    }


    }
}