using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Table;
using ProgramacionDistribuida.Common.Models;
using ProgramacionDistribuida.Common.Responses;
using System.Security.Principal;
using ProgramacionDistribuida.Function.Entities;

namespace ProgramacionDistribuida.Function.Functions
{
    public static class TodoAPI
    {
        [FunctionName(nameof(CreateTodo))]
        public static async Task<IActionResult> CreateTodo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Todo")] HttpRequest req,
            [Table("Todo", Connection = "AzureWebJobsStorage")] CloudTable TodoTable,
            ILogger log)
        {
            log.LogInformation("Recived a new todo");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Todo todo = JsonConvert.DeserializeObject<Todo>(requestBody);

            if (string.IsNullOrEmpty(todo?.TaskDescription)) 
            {
                return new BadRequestObjectResult(new Responses
                {
                    IsSuccess = false,
                    Message = "The request must have a TaskDescription."
                });
            }
            PDEntities todoEntity = new PDEntities
            {
                CreatedTime = DateTime.UtcNow,
                ETag = "*",
                IsCompleted = false,
                PartitionKey = "TODO",
                RowKey = Guid.NewGuid().ToString(),
                TaskDescription = todo.TaskDescription
            };

            TableOperation addOpertation = TableOperation.Insert(todoEntity);
            await TodoTable.ExecuteAsync(addOpertation);

            string message = "New todo stored in table";
            log.LogInformation(message);

            return new OkObjectResult(new Responses
            {
                IsSuccess = true,
                Message = message,
                Result = todoEntity
            });
        }

        [FunctionName(nameof(UpdateTodo))]
        public static async Task<IActionResult> UpdateTodo(
           [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "Todo/{id}")] HttpRequest req,
           [Table("Todo", Connection = "AzureWebJobsStorage")] CloudTable TodoTable,
           string id,
           ILogger log)
        {
            log.LogInformation($"Update for todo: {id}, received. ");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Todo todo = JsonConvert.DeserializeObject<Todo>(requestBody);

            // Validate todo id
            TableOperation finOperation = TableOperation.Retrieve<PDEntities>("TODO", id);
            TableResult finResult = await TodoTable.ExecuteAsync(finOperation);

            if(finResult.Result == null) 
            {
                return new BadRequestObjectResult(new Responses
                {
                    IsSuccess = false,
                    Message = "Todo not found."
                });
            }

            // Update todo

            PDEntities todoEntity = (PDEntities)finResult.Result;
            todoEntity.IsCompleted = todo.IsCompleted;
            if (string.IsNullOrEmpty(todo.TaskDescription))
            {
                todoEntity.TaskDescription = todo.TaskDescription;
            }

            TableOperation addOpertation = TableOperation.Replace(todoEntity);
            await TodoTable.ExecuteAsync(addOpertation);

            string message = $"Todo: {id}, updated in table";
            log.LogInformation(message);

            return new OkObjectResult(new Responses
            {
                IsSuccess = true,
                Message = message,
                Result = todoEntity
            });
        }
    }
}
