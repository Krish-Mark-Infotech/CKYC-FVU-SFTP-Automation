

using FVUFileMove.Services;
using Microsoft.Extensions.Configuration;

namespace FVUFileMove
{
    internal class Program
    {
        private const string Job4MutexName =
            @"Global\CKYC_FVU_JOB4_LOCK";

        static void Main(string[] args)
        {
            IConfiguration configuration =
                new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(
                        "appsettings.json",
                        optional: false,
                        reloadOnChange: false)
                    .Build();

            // Create logger
            LogService logger =
                new LogService(configuration);

            logger.Info("=================================");
            logger.Info("FVMUtility Started");
            logger.Info("=================================");

            Console.WriteLine("=================================");
            Console.WriteLine("       FVU File Move Started");
            Console.WriteLine("=================================");

            string mwBatchId =
                args.Length > 0
                    ? args[0]
                    : string.Empty;


            //string mwBatchId = "30924";
            logger.Info(
                "MWBatchID received: "
                + mwBatchId);

            if (string.IsNullOrWhiteSpace(mwBatchId))
            {
                logger.Error(
                    "MWBatchID is required.");

                Console.WriteLine(
                    "MWBatchID is required.");

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

            logger.Info(
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

                    logger.Warning(
                        "Previous Job4 process ended without releasing the lock. Continuing with current batch.");
                }

                if (!lockTaken)
                {
                    Console.WriteLine(
                        "Job4 lock wait timed out. Another Job4 process is still running.");

                    logger.Warning(
                        "Job4 lock wait timed out. Another Job4 process is still running.");

                    return;
                }

                Console.WriteLine(
                    "Job4 lock acquired.");

                logger.Info(
                    "Job4 lock acquired.");

                FVUProcessingService service =
                    new FVUProcessingService(
                        configuration,
                        logger);

                logger.Info(
                    "Starting FVUProcessingService.");

                service.ProcessMWBatch(
                    mwBatchId);

                logger.Info(
                    "FVUProcessingService completed.");
            }
            catch (Exception ex)
            {
                logger.Error(
                    "Unhandled exception in FVMUtility.",
                    ex);

                Console.WriteLine(
                    "Unhandled error: "
                    + ex.Message);
            }
            finally
            {
                if (lockTaken)
                {
                    mutex.ReleaseMutex();

                    Console.WriteLine(
                        "Job4 lock released.");

                    logger.Info(
                        "Job4 lock released.");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=================================");
            Console.WriteLine("       FVU File Move Completed");
            Console.WriteLine("=================================");

            logger.Info("FVMUtility Completed");
            logger.Info("=================================");
        }
    }
}
