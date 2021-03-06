﻿using System;
using ArchitectNow.Models.Exceptions;
using ArchitectNow.Models.ViewModels;
using Autofac.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ArchitectNow.Web.Services
{
    public class ExceptionResultBuilder : IExceptionResultBuilder
    {
	    private readonly IWebHostEnvironment _hostingEnvironment;
	    private readonly ILogger<ExceptionResultBuilder> _logger;

	    public ExceptionResultBuilder(IWebHostEnvironment hostingEnvironment, ILogger<ExceptionResultBuilder> logger)
	    {
		    _hostingEnvironment = hostingEnvironment;
		    _logger = logger;
	    }
        public IActionResult Build(Exception exception)
        {
            var stackTrace = "No stack trace available";

	        if (!string.Equals(_hostingEnvironment.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase))
            {
                stackTrace = exception.GetBaseException().StackTrace;
            }
            var statusCode = 500;
            string content = null;
            var message = exception.GetBaseException().Message;

	        if (exception is DependencyResolutionException)
            {
                message = $"Dependency Exception: Please ensure that classes implement the interface: {message}";
            }

	        if (exception is NotFoundException)
	        {
		        return new NotFoundResult();
	        }

	        if (exception is ApiException apiException)
            {
                statusCode = (int)apiException.StatusCode;
                content = apiException.GetContent();
                if (!string.IsNullOrEmpty(apiException.Message))
                {
                    message = apiException.GetBaseException().Message;
                }
            }

	        return CreateActionResult(content ?? string.Empty, message, stackTrace ?? string.Empty, statusCode, exception);
        }

	    protected virtual IActionResult CreateActionResult(string content, string message, string stackTrace, int statusCode, Exception exception)
	    {
		    var apiError = new ApiError
		    {
			    Error = content ?? message
		    };

		    if (!string.IsNullOrEmpty(stackTrace))
		    {
			    apiError.StackTrace = stackTrace;
		    }

		    var objectResult = new ObjectResult(apiError)
		    {
			    StatusCode = statusCode
		    };
		    var eventId = new EventId(statusCode);

		    _logger.LogError(eventId, exception, message);

		    return objectResult;
	    }
    }
}