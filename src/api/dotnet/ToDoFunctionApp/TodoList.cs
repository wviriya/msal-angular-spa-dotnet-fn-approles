using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using MongoDB.Driver;
using AzninjaTodoFn.Models;
using AzninjaTodoFn.Helpers;
using Microsoft.OpenApi.Models;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Identity.Web;

namespace AzninjaTodoFn
{
    public class TodoList
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private MongoClient _client;
        private readonly IMongoCollection<TodoItem> _todolist;
        private IEnumerable<string> _roles;
        static readonly string[] adminRole = new string[] {"TaskAdmin","TaskUser"};
        static readonly string[] userRole = new string[] {"TaskUser"};

        public TodoList(ILogger<TodoList> logger, IConfiguration config, MongoClient client)
        {
            _logger = logger;
            _config = config;
            _client = client;
            var database = _client.GetDatabase(_config[AzninjaTodoFn.Helpers.Constants.databaseName]);
            _todolist = database.GetCollection<TodoItem>(_config[AzninjaTodoFn.Helpers.Constants.collectionName]);
        }

        [OpenApiOperation(operationId: "GetTodoItems")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TodoItem[]), Description = "A to do list")]
        [FunctionName("GetTodoItems")]
        public async Task<IActionResult> GetTodoItems(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos")]
            HttpRequest req)
        {
            try
            {
                req.Authorize(userRole);

                var result = (await _todolist.FindAsync(item => item.Owner == req.UserName())).ToList();

                if (!result.Any())
                {
                    _logger.LogInformation($"There are no items in the collection");
                    return new NotFoundObjectResult($"There are no items in the collection");
                }
                
                return new OkObjectResult(result);   
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError($"Exception thrown: {ex.Message}");
                return new UnauthorizedObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception thrown: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [OpenApiOperation(operationId: "GetAll")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TodoItem[]), Description = "A to do list")]
        [FunctionName("GetAll")]
        public async Task<IActionResult> GetAll(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "todos/all")]
            HttpRequest req)
        {
           
            try
            {
                req.Authorize(adminRole);

                var result = (await _todolist.FindAsync(_ => true)).ToList();

                if (!result.Any())
                {
                    _logger.LogInformation($"There are no items in the collection");
                    return new NotFoundObjectResult($"There are no items in the collection");
                }
                
                return new OkObjectResult(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError($"Exception thrown: {ex.Message}");
                return new UnauthorizedObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception thrown: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [OpenApiOperation(operationId: "GetTodoItem")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "To do item id")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TodoItem), Description = "A to do item")]
        [FunctionName("GetTodoItem")]
        public async Task<IActionResult> GetTodoItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", 
                Route = "todos/{id}")] HttpRequest req, string id)
        {
            try
            {
                    req.Authorize(userRole);
               
                    var result = await _todolist.FindAsync(item => item.Id == id && item.Owner == req.UserName());

                    if (!result.Any())
                    {
                        _logger.LogWarning("That item doesn't exist!");
                        return new NotFoundObjectResult("That item doesn't exist!");
                    }
                    
                    return new OkObjectResult(result.FirstOrDefault());
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError($"Exception thrown: {ex.Message}");
                return new UnauthorizedObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Couldn't find item with id: {id}. Exception thrown: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [OpenApiOperation(operationId: "PostTodoItem")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(TodoItem), Required = true, Description = "To do object that needs to be added to the list")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TodoItem), Description = "A to do item")]
        [FunctionName("PostTodoItem")]
        public async Task<IActionResult> PostTodoItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todos")] HttpRequest req)
        {
            try
            {
                    req.Authorize(userRole);

                    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                    var input = JsonConvert.DeserializeObject<TodoItem>(requestBody);

                    var todo = new TodoItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Description = input.Description,
                        Owner = req.UserName(),
                        Status = false
                    };

                    _todolist.InsertOne(todo);

                    _logger.LogInformation("Todo item inserted");
                    return new OkObjectResult(todo);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError($"Exception thrown: {ex.Message}");
                return new UnauthorizedObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not insert item. Exception thrown: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [OpenApiOperation(operationId: "PutTodoItem")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "To do Id")]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(TodoItem), Required = true, Description = "To do object that needs to be updated to the list")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TodoItem), Description = "A to do item")]
        [FunctionName("PutTodoItem")]
        public async Task<IActionResult> PutTodoItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "todos/{id}")] HttpRequest req,
            string id)
        {            
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var updatedResult = JsonConvert.DeserializeObject<TodoItem>(requestBody);

            updatedResult.Id = id;

            try
            {
                    req.Authorize(userRole);
                
                    var replacedItem = _todolist.ReplaceOneAsync(item => item.Id == id && item.Owner == req.UserName(), updatedResult);

                    if (replacedItem.IsFaulted)
                    {
                        _logger.LogInformation($"Todo item with id: {id} does not exist. Update failed");
                        return new NotFoundObjectResult($"Todo item with id: {id} does not exist. Update failed");
                    }

                    return new OkObjectResult(updatedResult);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError($"Exception thrown: {ex.Message}");
                return new UnauthorizedObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not update Album with id: {id}. Exception thrown: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

       
        [OpenApiOperation(operationId: "DeleteTodoItem")]
        [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "To do Id that needs to be removed from the list")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "OK Response")]
        [FunctionName("DeleteTodoItem")]
        public async Task<IActionResult> DeleteTodoItem(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete",
            Route = "todos/{id}")]HttpRequest req, string id)
        {

            try
            {
                    req.Authorize(userRole);
               
                    var itemToDelete = await _todolist.DeleteOneAsync(item => item.Id == id && item.Owner == req.UserName());

                    if (itemToDelete.DeletedCount == 0)
                    {
                        _logger.LogInformation($"Todo item with id: {id} does not exist. Delete failed");
                        return new NotFoundObjectResult($"Todo item with id: {id} does not exist. Delete failed");
                    }

                    return new StatusCodeResult(StatusCodes.Status200OK);

            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError($"Exception thrown: {ex.Message}");
                return new UnauthorizedObjectResult(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Could not delete item. Exception thrown: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }



        [OpenApiOperation(operationId: "HealthCheck")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Description = "Status code 200")]
        [FunctionName("HealthCheck")]
        public async Task<IActionResult> HealthCheck(
            [HttpTrigger(AuthorizationLevel.Anonymous, "head",
            Route = "todos")]HttpRequestMessage req)
        {
            return new StatusCodeResult(StatusCodes.Status200OK);
        }
    }
}
