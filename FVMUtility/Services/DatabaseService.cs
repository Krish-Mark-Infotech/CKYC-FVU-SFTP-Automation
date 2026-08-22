    using FVMUtility.Models;
    using Microsoft.Data.SqlClient;
    using Microsoft.Extensions.Configuration;
using System.Data;

    namespace FVUFileMove.Services
    {
        public class DatabaseService
        {
            private readonly string _connectionString;
        private readonly string _upddwnldConnectionString;
        private readonly LogService _logger;
        public DatabaseService(IConfiguration configuration, LogService logger)
            {

            _logger = logger;
            _connectionString =
                    configuration.GetConnectionString("CKYCDataBase")
                    ?? throw new InvalidOperationException(
                        "CKYCDataBase connection string is not configured.");


            _upddwnldConnectionString =
      configuration.GetConnectionString("UPDDWNLDataBase")
      ?? throw new InvalidOperationException(
          "UPDDWNLDataBase connection string not found.");

        }


            public string GetCSRFilePath(string transactionType)
            {
                const string query = @"
            SELECT TOP 1
                File_Name,
                CSRFilepath
            FROM UPDDWNLD..InitialConfiguration WITH (NOLOCK)
            WHERE File_Name = @TransactionType";

                using SqlConnection connection =
                    new SqlConnection(_connectionString);

                using SqlCommand command =
                    new SqlCommand(query, connection);

                command.Parameters.AddWithValue(
                    "@TransactionType",
                    transactionType);

                connection.Open();

                using SqlDataReader reader =
                    command.ExecuteReader();

                if (reader.Read())
            //{
            //    return reader["CSRFilepath"]
            //        ?.ToString()
            //        ?.Trim()
            //        ?? string.Empty;
            //}


            {
                string csrFilePath =
                    reader["CSRFilepath"]
                        ?.ToString()
                        ?.Trim()
                        ?? string.Empty;

                _logger.Info(
                    "DB: CSR file path found for TransactionType: "
                    + transactionType
                    + " | Path: "
                    + csrFilePath);

                return csrFilePath;
            }

            _logger.Warning(
            "DB: No CSR file path found for TransactionType: "
            + transactionType);

            return string.Empty;
            }
            public List<BatchDetails> GetPendingBatches(string mwBatchId)
            {

            _logger.Info(
               "DB: Fetching pending batches for MWBatchID: "
               + mwBatchId);


            const string query = @"
                    SELECT
                        ID,
                        MWBatchID,
                        CSRBatchID,
                        TransactionType,
                        Status
                    FROM ckyc..MW_CSR_BATCH_DTLS WITH (NOLOCK)
                    WHERE MWBatchID = @MWBatchID
                      AND Status = 'P'
                    ORDER BY ID";

                List<BatchDetails> batches = new List<BatchDetails>();

                using (SqlConnection connection =
                       new SqlConnection(_connectionString))
                using (SqlCommand command =
                       new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue(
                        "@MWBatchID",
                        mwBatchId);

                    connection.Open();

                    using (SqlDataReader reader =
                           command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            batches.Add(new BatchDetails
                            {
                                ID = Convert.ToInt32(reader["ID"]),

                                MWBatchID =
                                    Convert.ToInt32(reader["MWBatchID"]),

                                CSRBatchID =
                                    Convert.ToInt32(reader["CSRBatchID"]),

                                TransactionType =
                                    reader["TransactionType"].ToString(),

                                Status =
                                    reader["Status"].ToString()
                            });
                        }
                    }
                }

            _logger.Info(
                      "DB: Pending batches returned for MWBatchID "
                      + mwBatchId
                      + ": "
                      + batches.Count);
            return batches;
            }



            public void UpdateVFToPending(int csrBatchId)
            {


            _logger.Info(
              "DB: Updating CSRBatchID "
              + csrBatchId
              + " status VF -> P");

            const string query = @"
            UPDATE ckyc..MW_CSR_BATCH_DTLS
            SET
                Status = 'P',
                UpdatedDateTime = @UpdatedDateTime
            WHERE CSRBatchID = @CSRBatchID
              AND Status = 'VF'";

                using SqlConnection connection =
                    new SqlConnection(_connectionString);

                using SqlCommand command =
                    new SqlCommand(query, connection);

                command.Parameters.AddWithValue(
                    "@CSRBatchID",
                    csrBatchId);

                command.Parameters.AddWithValue(
                    "@UpdatedDateTime",
                    DateTime.Now);

                connection.Open();

            int rowsAffected = command.ExecuteNonQuery();
            _logger.Info(
              "DB: CSRBatchID "
              + csrBatchId
              + " VF -> P update completed. Rows affected: "
              + rowsAffected);

        }

            public void UpdateBatchStatus(
        int csrBatchId,
        string status,
        string errorDesc = null,
        string fvuOutputFileDtls = null)
            {

            _logger.Info(
              "DB: Updating CSRBatchID "
              + csrBatchId
              + " status to "
              + status);

            const string query = @"
            UPDATE ckyc..MW_CSR_BATCH_DTLS
            SET
                Status = @Status,
                ErrorDesc = @ErrorDesc,
                FVUOutputFileDtls = COALESCE(@FVUOutputFileDtls, FVUOutputFileDtls),
                UpdatedDateTime = @UpdatedDateTime
            WHERE CSRBatchID = @CSRBatchID";

                DateTime updatedDateTime = DateTime.Now;

                using (SqlConnection connection =
                       new SqlConnection(_connectionString))
                using (SqlCommand command =
                       new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue(
                        "@CSRBatchID",
                        csrBatchId);

                    command.Parameters.AddWithValue(
                        "@Status",
                        status);

                    command.Parameters.AddWithValue(
                        "@ErrorDesc",
                        (object)errorDesc ?? DBNull.Value);

                    command.Parameters.AddWithValue(
                        "@FVUOutputFileDtls",
                        string.IsNullOrWhiteSpace(fvuOutputFileDtls)
                            ? (object)DBNull.Value
                            : fvuOutputFileDtls);

                    command.Parameters.AddWithValue(
                        "@UpdatedDateTime",
                        updatedDateTime);

                    connection.Open();

                   int rowsAffected = command.ExecuteNonQuery();

                _logger.Info(
                "DB: CSRBatchID "
                + csrBatchId
                + " status updated to "
                + status
                + ". Rows affected: "
                + rowsAffected);
            }
            }





        public void UpdateBatchWiseAuditDetailsForFileGeneration(
         string mwBatchId,
         string createdBy,
         string message)
        {
            using SqlConnection con =
                new SqlConnection(_upddwnldConnectionString);

            con.Open();

            using SqlCommand cmd =
                new SqlCommand(
                    "usp_InsertBatchAuditTrail",
                    con);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@MWBatchID", mwBatchId);
            cmd.Parameters.AddWithValue("@Message", message);
            cmd.Parameters.AddWithValue("@CreatedBy", createdBy);

            cmd.ExecuteNonQuery();
        }
    }

    }
