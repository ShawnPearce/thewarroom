using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace TheWarRoomApp.Infrastructure
{
    // When the API project is not running, every HttpClient call throws HttpRequestException.
    // This filter shows a page that says so, instead of the general error page.
    public class ApiUnavailableFilter : IExceptionFilter
    {
        private readonly IModelMetadataProvider _metadataProvider;

        public ApiUnavailableFilter(IModelMetadataProvider metadataProvider)
        {
            _metadataProvider = metadataProvider;
        }

        public void OnException(ExceptionContext context)
        {
            if (context.Exception is not HttpRequestException)
            {
                return;
            }

            context.Result = new ViewResult
            {
                ViewName = "ApiUnavailable",
                StatusCode = StatusCodes.Status503ServiceUnavailable,
                ViewData = new ViewDataDictionary(_metadataProvider, context.ModelState)
            };
            context.ExceptionHandled = true;
        }
    }
}
