using FVUFileMove.Services;
using Microsoft.Extensions.Configuration;

namespace FVUFileMove
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            IConfiguration configuration =
                new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(
                        "appsettings.json",
                        optional: false,
                        reloadOnChange: false)
                    .Build();

            Console.WriteLine("=================================");
            Console.WriteLine("       FVU File Move Started");
            Console.WriteLine("=================================");

            //string mwBatchId =
            //    args.Length > 0
            //        ? args[0]
            //        : string.Empty;

            string mwBatchId = "30399";

            if (string.IsNullOrWhiteSpace(mwBatchId))
            {
                Console.WriteLine("MWBatchID is required.");
                return;
            }

            Console.WriteLine(
                "MWBatchID: " + mwBatchId);

            FVUProcessingService service =
                new FVUProcessingService(configuration);

            await service.ProcessMWBatch(mwBatchId);

            Console.WriteLine();
            Console.WriteLine("=================================");
            Console.WriteLine("       FVU File Move Completed");
            Console.WriteLine("=================================");
        }
    }
}
