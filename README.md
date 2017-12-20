# Cross Region Blob Copy

## Purpose

These scripts are meant to be used in Azure Functions to facilitate copying blobs from a storage account in one region to a storage account in another region based on a set
schedule. While GRS storage exists in Azure, you do not have the ability to select your backup region, this aims to solve that. Don't treat this as a real solution or a
production ready "app". This is just a PoC/demo on how you might solve this with Azure Functions.


Additionally, take note that this is what happens when you ask a go person to write C#.


## Process

The process is fairly simple and is meant to use as many PaaS services as possible to avoid management overhead. The idea was to keep things as simple as possible, hence
the use of Azure Table Storage vs something like Azure SQL.

The copy function simply runs on a set schedule and takes all of the blobs from one storage container and begins the copy process to another container. This is an async
process so understand that this function is simply starting the copy process, not following it through to completion. Each copy process is entered into an Azure table store
with a status of "PENDING".

The status function runs on a timer as well and is responsible for checking the status of the copy process. It will look at the Azure table store for any "jobs" that are
in a "PENDING" state and then query them to get their current status. If they are complete then the table is updated to reflect that. If they are still in progress a 
length of time is calculated (how long the copy has been going on for) and the user can set an action based on that result. This might be something like sending off
an e-mail, calling a monitoring API, whatever.


## Setup

These functions need access to storage account credentials so we put them in the application settings section of the function app. The following variables need to exist:

* SRC_STG_ACCT
* SRC_STG_KEY
* DEST_STG_ACCT
* DEST_STG_KEY
* TABLE_STG_ACCT
* TABLE_STG_KEY


The assumption is that three storage accounts are being used but this certainly isn't a requirement. You could easily put the table into either the source or destination storage account.


## !! WARNING !!

There is some hardcoded values still in the code so if you are going to change things make sure you are updating all of the references. Again, this is just sample code.

Additionally, if you are copying vhd files they must NOT have a lease on them. The whole lease process does not exist in this code as the assumption is that you are copying
data that is not "live".
