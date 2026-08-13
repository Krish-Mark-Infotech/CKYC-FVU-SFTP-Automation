using FVMUtility.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FVUFileMove.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("CKYCDataBase")
                ?? throw new InvalidOperationException(
                    "CKYCDataBase connection string is not configured.");
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
            {
                return reader["CSRFilepath"]
                    ?.ToString()
                    ?.Trim()
                    ?? string.Empty;
            }

            return string.Empty;
        }
        public List<BatchDetails> GetPendingBatches(string mwBatchId)
        {
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

            return batches;
        }



        public void UpdateVFToPending(int csrBatchId)
        {
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

            command.ExecuteNonQuery();
        }

        public void UpdateBatchStatus(
    int csrBatchId,
    string status,
    string errorDesc = null)
        {
            const string query = @"
        UPDATE ckyc..MW_CSR_BATCH_DTLS
        SET
            Status = @Status,
            ErrorDesc = @ErrorDesc,
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
                    "@UpdatedDateTime",
                    updatedDateTime);

                connection.Open();

                command.ExecuteNonQuery();
            }
        }




    }

}