using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProjectApprovalAPI.Exceptions;

namespace ProjectApprovalAPI.Common
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            int statusCode;
            string message;

            switch (exception)
            {
                case InvalidOperationException:
                    statusCode = StatusCodes.Status400BadRequest;
                    message = exception.Message;
                    break;

                case ArgumentNullException:
                case ArgumentException:
                    statusCode = StatusCodes.Status400BadRequest;
                    message = exception.Message;
                    break;
                case BusinessException:
                    statusCode = StatusCodes.Status400BadRequest;
                    message = exception.Message;
                    break;

                default:
                    statusCode = StatusCodes.Status500InternalServerError;
                    message = "Ha ocurrido un error inesperado.";
                    break;
            }

            context.Response.StatusCode = statusCode;

            var error = new ApiError(message);

            return context.Response.WriteAsJsonAsync(error);
        }
    }

}
