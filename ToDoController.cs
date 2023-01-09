using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Documents.Client;
using System.Collections.Generic;
using Microsoft.Azure.Documents.Linq;
using System.Linq;

namespace ToDoList
{
    public static class ToDoController
    {

        const string _DATABASE = "ToDoList";
        const string _CONTAINER = "Task";
        private static string CONNECTIONSTRING = Environment.GetEnvironmentVariable("CosmosDBConnection");

        [FunctionName("GetListTask")]
        public static async Task<IActionResult> GetListTask(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "task/list")] HttpRequest req,
            [CosmosDB(ConnectionStringSetting = "CosmosDBConnection")] DocumentClient client,
            ILogger log)
        {
            try
            {
                
                var result = new List<Model.Task>();

                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(_DATABASE, _CONTAINER);
                var options = new FeedOptions() { EnableCrossPartitionQuery = true };

                IDocumentQuery<Model.Task> query = client.CreateDocumentQuery<Model.Task>(collectionUri, feedOptions: options)
                    .Where(p => p.Id != null)
                    .AsDocumentQuery();

                while (query.HasMoreResults)
                {
                    foreach (Model.Task item in await query.ExecuteNextAsync())
                    {
                        log.LogInformation(item.Message);
                        result.Add(item);
                    }
                }
                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                return new BadRequestObjectResult(ex.Message);
            }
        }

        [FunctionName("CreateTask")]
        public static async Task<IActionResult> CreateTask(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "task")] HttpRequest req,
            ILogger log)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var newTask = JsonConvert.DeserializeObject<Model.Task>(requestBody);

                newTask.Id = Guid.NewGuid().ToString();
                newTask.CreatedDate = DateTime.Now;
                newTask.ModifiedDate = DateTime.Now;

                var client = new CosmosClient(CONNECTIONSTRING);
                Container cosmosContainer = client.GetDatabase(_DATABASE).GetContainer(_CONTAINER);

                var createdItem = await cosmosContainer.CreateItemAsync(newTask);

                return new OkObjectResult(createdItem.StatusCode);
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                return new BadRequestObjectResult(ex.Message);
                throw;

            }
        }

        [FunctionName("UpdateTaskById")]
        public static async Task<IActionResult> UpdateTaskById(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "task/update/{id}")] HttpRequest req,
            [CosmosDB(ConnectionStringSetting = "CosmosDBConnection")] DocumentClient client,
            [CosmosDB(
            databaseName:_DATABASE,
            collectionName:_CONTAINER,
            ConnectionStringSetting = "CosmosDBConnection",
            Id = "{id}",
            PartitionKey = "{id}")] Model.Task oldTask,
            string id,
            ILogger log
            )
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var taskUpdate = JsonConvert.DeserializeObject<Model.Task>(requestBody);
                oldTask.Message = taskUpdate.Message;
                oldTask.ModifiedDate = DateTime.Now;
                

                var result = await client.ReplaceDocumentAsync(
                    UriFactory.CreateDocumentUri(_DATABASE, _CONTAINER, id), oldTask);

                return new OkObjectResult(result.StatusCode);
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                return new BadRequestObjectResult(ex.Message);
                throw;
            }
        }

        [FunctionName("DeleteTaskById")]
        public static async Task<IActionResult> DeleteTaskById(
           [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "task/delete/{id}")] HttpRequest req,
           [CosmosDB(
            databaseName:_DATABASE,
            collectionName:_CONTAINER,
            ConnectionStringSetting = "CosmosDBConnection",
            Id = "{id}",
            PartitionKey = "{id}")] Model.Task task,
           ILogger log
           )
        {
            try
            {
                log.LogInformation(task.Message + " will be deleted.");

                if (task == null) throw new ArgumentException("Error delete data, old data not found");

                var client = new CosmosClient(CONNECTIONSTRING);
                Container cosmosContainer = client.GetDatabase(_DATABASE).GetContainer(_CONTAINER);

                var information = await cosmosContainer.DeleteItemAsync<Model.Task>(task.Id, new PartitionKey(task.Id));

                return new OkObjectResult(information.StatusCode);
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                return new BadRequestObjectResult(ex.Message);
                throw;
            }
        }
    }
}
