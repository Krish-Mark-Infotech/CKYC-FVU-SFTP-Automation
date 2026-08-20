using FVUFileMove.Services;
using Microsoft.Extensions.Configuration;

namespace FVUFileMove
{
    internal class Program
    {
        private const string Job4MutexName = @"Global\CKYC_FVU_JOB4_LOCK";

        static  void Main(string[] args)
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
            string mwBatchId = "30770";


            if (string.IsNullOrWhiteSpace(mwBatchId))
            {
                Console.WriteLine("MWBatchID is required.");
                return;
            }

            int lockWaitMinutes =
                configuration.GetValue<int?>(
                    "ProcessingSettings:Job4LockWaitMinutes")
                ?? 240;

            if (lockWaitMinutes <= 0)
            {
                lockWaitMinutes = 240;
            }

            Console.WriteLine(
                "MWBatchID: " + mwBatchId);

            Console.WriteLine(
                "Waiting for Job4 lock. Timeout minutes: "
                + lockWaitMinutes);

            using Mutex mutex =
                new Mutex(
                    false,
                    Job4MutexName);

            bool lockTaken = false;

            try
            {
                try
                {
                    lockTaken =
                        mutex.WaitOne(
                            TimeSpan.FromMinutes(
                                lockWaitMinutes));
                }
                catch (AbandonedMutexException)
                {
                    lockTaken = true;

                    Console.WriteLine(
                        "Previous Job4 process ended without releasing the lock. Continuing with current batch.");
                }

                if (!lockTaken)
                {
                    Console.WriteLine(
                        "Job4 lock wait timed out. Another Job4 process is still running.");

                    return;
                }

                Console.WriteLine(
                    "Job4 lock acquired.");

                FVUProcessingService service =
                    new FVUProcessingService(configuration);

                 service.ProcessMWBatch(mwBatchId);
            }
            finally
            {
                if (lockTaken)
                {
                    mutex.ReleaseMutex();

                    Console.WriteLine(
                        "Job4 lock released.");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=================================");
            Console.WriteLine("       FVU File Move Completed");
            Console.WriteLine("=================================");
        }
    }
}