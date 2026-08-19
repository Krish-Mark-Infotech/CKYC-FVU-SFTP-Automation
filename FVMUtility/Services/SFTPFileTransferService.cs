using Microsoft.Extensions.Configuration;
using Renci.SshNet;

namespace FVUFileMove.Services
{
    public class SFTPFileTransferService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _uploadPath;

        public SFTPFileTransferService(IConfiguration configuration)
        {
            _host = configuration["SFTPServerB:Host"]
                ?? throw new Exception("SFTP Server B Host is missing.");

            _port = int.Parse(
                configuration["SFTPServerB:Port"] ?? "22");

            _username = configuration["SFTPServerB:Username"]
                ?? throw new Exception("SFTP Server B Username is missing.");

            _password = configuration["SFTPServerB:Password"]
                ?? throw new Exception("SFTP Server B Password is missing.");

            _uploadPath = configuration["SFTPServerB:UploadPath"]
                ?? throw new Exception("SFTP Server B UploadPath is missing.");
        }

        public void UploadFile(string localFilePath)
        {
            if (!File.Exists(localFilePath))
            {
                throw new FileNotFoundException(
                    "File not found.",
                    localFilePath);
            }

            using var sftp = new SftpClient(
                _host,
                _port,
                _username,
                _password);

            Console.WriteLine(
                $"Connecting to SFTP Server B: {_host}:{_port}");

            sftp.Connect();

            if (!sftp.IsConnected)
            {
                throw new Exception(
                    "Unable to connect to SFTP Server B.");
            }

            Console.WriteLine("Connected to SFTP Server B.");

            string fileName = Path.GetFileName(localFilePath);

            string remoteFilePath =
                $"{_uploadPath.TrimEnd('/')}/{fileName}";

            Console.WriteLine(
                $"Uploading: {fileName}");

            using FileStream fileStream =
                File.OpenRead(localFilePath);

            sftp.UploadFile(
                fileStream,
                remoteFilePath);

            Console.WriteLine(
                $"Upload successful: {remoteFilePath}");

            sftp.Disconnect();
        }
    }
}