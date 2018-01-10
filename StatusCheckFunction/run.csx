#r "Microsoft.WindowsAzure.Storage"

using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Microsoft.WindowsAzure.Storage.Auth;
using System.IO;
using System.Threading.Tasks;

public static void Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"C# Timer trigger function executed at: {DateTime.Now}");
    try {
        string tableAcct = System.Environment.GetEnvironmentVariable("TABLE_STG_ACCT", EnvironmentVariableTarget.Process);
        string tableKey = System.Environment.GetEnvironmentVariable("TABLE_STG_KEY", EnvironmentVariableTarget.Process);
        string destAcct = System.Environment.GetEnvironmentVariable("DEST_STG_ACCT", EnvironmentVariableTarget.Process);
        string destKey = System.Environment.GetEnvironmentVariable("DEST_STG_KEY", EnvironmentVariableTarget.Process);

        // blob stuff
        CloudStorageAccount destStorageAccount = new CloudStorageAccount(new StorageCredentials((string)destAcct.Trim(), (string)destKey.Trim()), true);
        CloudBlobClient destCloudBlobClient = destStorageAccount.CreateCloudBlobClient();
        CloudBlobContainer destContainer = destCloudBlobClient.GetContainerReference("sqlcopy");

        // table stuff
        // Retrieve the storage account from the connection string.
        CloudStorageAccount storageAccount = new CloudStorageAccount(new StorageCredentials((string)tableAcct.Trim(), (string)tableKey.Trim()), true);

        // Create the table client.
        CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

        // Create the CloudTable object that represents the table.
        CloudTable table = tableClient.GetTableReference("outTable");

        // Create the table query.
        TableQuery<BlobBackupEntity> query = new TableQuery<BlobBackupEntity>().Where(
            TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("Status", QueryComparisons.Equal, "PENDING"),
                TableOperators.And,
                TableQuery.GenerateFilterCondition("Container", QueryComparisons.Equal, destContainer.Name)));
  
        // Loop through the results, displaying information about the entity.
        foreach (BlobBackupEntity entity in table.ExecuteQuery(query))
        {
                CloudBlob destBlob = (CloudBlob)destContainer.GetBlobReferenceFromServer(entity.Name);
  
                if (destBlob.CopyState.Status != CopyStatus.Success)
                {
                    log.Info("blob copy has not completed for " + entity.Name);
                    // TODO: call web API, email, something to alert based on time
                    var minutes = (DateTime.UtcNow - entity.Timestamp).TotalHours;
                    log.Info("copy time in minutes: " + minutes);
                } else {
                    // update status
                    // Create a retrieve operation that takes a customer entity.
                    List<string> columnList = new List<string>();
                    columnList.Add("Name");
                    TableOperation retrieveOperation = TableOperation.Retrieve<BlobBackupEntity>(entity.PartitionKey, entity.RowKey, columnList);

                    // Execute the operation.
                    TableResult retrievedResult = table.Execute(retrieveOperation);

                    // Assign the result to a CustomerEntity object.
                    BlobBackupEntity updateEntity = (BlobBackupEntity)retrievedResult.Result;

                    if (updateEntity != null)
                    {
                        // Change the status.
                        updateEntity.Status = "COMPLETE";

                        // Create the Replace TableOperation.
                        TableOperation updateOperation = TableOperation.Replace(updateEntity);

                        // Execute the operation.
                        table.Execute(updateOperation);

                        log.Info("Entity updated.");
                    }
                    else
                    {
                        log.Info("Entity could not be retrieved.");
                    }
                }
        }

    } catch (Exception ex) { 
        log.Info(ex.ToString()); if (null != ex.InnerException) { log.Info(ex.InnerException.ToString()); } 
    }
    log.Info($"C# Timer trigger function completed at: {DateTime.Now}");
}

public class BlobBackupEntity : TableEntity
{
    public BlobBackupEntity() { }
    public string Name { get; set; }
    public string Container { get; set; }
    public string Status { get; set; }
}
