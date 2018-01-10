#r "Microsoft.WindowsAzure.Storage"

using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Microsoft.WindowsAzure.Storage.Auth;
using System.IO;
using System.Threading.Tasks;

public static void Run(TimerInfo myTimer, ICollector<BlobBackup> outputTable, TraceWriter log)
{
    log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

    try {
        string srcAcct = System.Environment.GetEnvironmentVariable("SRC_STG_ACCT", EnvironmentVariableTarget.Process);
        string srcKey = System.Environment.GetEnvironmentVariable("SRC_STG_KEY", EnvironmentVariableTarget.Process);
        string destAcct = System.Environment.GetEnvironmentVariable("DEST_STG_ACCT", EnvironmentVariableTarget.Process);
        string destKey = System.Environment.GetEnvironmentVariable("DEST_STG_KEY", EnvironmentVariableTarget.Process);

        CloudStorageAccount sourceStorageAccount = new CloudStorageAccount(new StorageCredentials((string)srcAcct.Trim(), (string)srcKey.Trim()), true);
        CloudBlobClient sourceCloudBlobClient = sourceStorageAccount.CreateCloudBlobClient();
        CloudBlobContainer sourceContainer = sourceCloudBlobClient.GetContainerReference("vhds");
        
        CloudStorageAccount destStorageAccount = new CloudStorageAccount(new StorageCredentials((string)destAcct.Trim(), (string)destKey.Trim()), true);
        CloudBlobClient destCloudBlobClient = destStorageAccount.CreateCloudBlobClient();
        CloudBlobContainer destContainer = destCloudBlobClient.GetContainerReference("sqlcopy");
        destContainer.CreateIfNotExists();
    
        // Create a policy for reading the blob.
        var policy = new SharedAccessBlobPolicy {
            Permissions = SharedAccessBlobPermissions.Read,
            SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-15),
            SharedAccessExpiryTime = DateTime.UtcNow.AddDays(1)
        };
        
        // loop over container
        // This assumes the VHD you are copying is NOT in use
        // as no lease is acquired
        foreach (var b in sourceContainer.ListBlobs()) {
            CloudBlob blob = (CloudBlob)b;
            // Get SAS of that policy.
            var sourceBlobToken = blob.GetSharedAccessSignature(policy);

            // Make a full uri with the sas for the blob.
            var sourceBlobSAS = string.Format("{0}{1}", blob.Uri, sourceBlobToken);
            
            CloudBlob destBlob = (CloudBlob)destContainer.GetBlobReference(blob.Name);
            // Turns out copying across SAs requires SAS tokens despite
            // it not being mentioned anywhere in the API docs >:|
            destBlob.StartCopy(new Uri(sourceBlobSAS));

            log.Info($"Adding BlobBackup entity");
            outputTable.Add(
                new BlobBackup() { 
                PartitionKey = "Test", 
                RowKey = blob.Name + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), 
                Name = blob.Name,
                Container = destContainer.Name,
                Status = "PENDING" }
            );
        }
    } catch (Exception ex) { 
        log.Info(ex.ToString()); if (null != ex.InnerException) { log.Info(ex.InnerException.ToString()); } 
    }

    log.Info($"C# Timer trigger function completed at: {DateTime.Now}");
}

public class BlobBackup
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public string Name { get; set; }
    public string Container { get; set; }
    public string Status { get; set; }
}
