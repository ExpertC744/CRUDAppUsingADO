
using System.Net;
using System.Text.Json;

namespace CRUDAppUsingADO.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger)
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
                _logger.LogError(
                    ex,
                    "Unhandled exception for {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path
                );

                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();

                context.Response.StatusCode =
                    (int)HttpStatusCode.InternalServerError;

                context.Response.ContentType = "application/json";

                var result = JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "An unexpected server error occurred."
                });

                await context.Response.WriteAsync(result);
            }
        }
    }
}

//using System.Net;
//using System.Text.Json;

//namespace CRUDAppUsingADO.Middleware
//{
//    public class ExceptionMiddleware
//    {
//        private readonly RequestDelegate _next;
//        private readonly ILogger<ExceptionMiddleware> _logger;

//        public ExceptionMiddleware(RequestDelegate next,
//                                   ILogger<ExceptionMiddleware> logger)
//        {
//            _next = next;
//            _logger = logger;
//        }

//        // This method MUST exist
//        public async Task InvokeAsync(HttpContext context)
//        {
//            try
//            {
//                await _next(context);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, ex.Message);

//                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
//                context.Response.ContentType = "application/json";

//                var result = JsonSerializer.Serialize(new
//                {
//                    Success = false,
//                    Message = ex.Message
//                });

//                await context.Response.WriteAsync(result);
//            }
//        }
//    }
//}