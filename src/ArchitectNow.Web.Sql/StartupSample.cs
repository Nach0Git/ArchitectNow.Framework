﻿using System;
using System.Reflection;
using System.Text;
using ArchitectNow.Models.Security;
using ArchitectNow.Services;
using ArchitectNow.Web.Configuration;
using ArchitectNow.Web.Models;
using ArchitectNow.Web.Sql.Configuration;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using Serilog;

namespace ArchitectNow.Web.Sql
{
	public sealed class StartupSample
    {
        private readonly ILogger<StartupSample> _logger;
        private readonly IConfiguration _configuration;
        private IContainer _applicationContainer;

        public StartupSample(ILogger<StartupSample> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            _logger.LogInformation($"{nameof(ConfigureServices)} starting...");
            
            services.ConfigureOptions();
            
            services.ConfigureJwt(_configuration, ConfigureSecurityKey);

            services.ConfigureApi(new FluentValidationOptions {Enabled = true});

            services.ConfigureAutomapper(expression => { });

            services.ConfigureCompression();

            //Register startup filters (order matters)
            services.AddTransient<IStartupFilter, AntiForgeryStartupFilter>();
            services.AddTransient<IStartupFilter, SwaggerStartupFilter>(serviceProvider =>
            {
                return new SwaggerStartupFilter( serviceProvider.GetService<ILogger<SwaggerStartupFilter>>(),
                    new SwaggerOptions
                {
                    Version = "1.0",
                    Title = "API",
                    Description = "API",
                    Name = "v1",
                    SwaggerRoute = "/app/docs/v1/swagger.json",
                    SwaggerUiRoute = "/app/docs",
                    Configure = settings =>
                    {
                        settings.UseJsonEditor = false;
                        settings.GeneratorSettings.DefaultEnumHandling = EnumHandling.String;
                        settings.GeneratorSettings.DefaultPropertyNameHandling = PropertyNameHandling.CamelCase;
                        settings.GeneratorSettings.Version = Assembly.GetEntryAssembly().GetName().Version.ToString();
                    },
                });
            });

            //last
            services.AddTransient<IStartupFilter, HangfireStartupFilter>();

	        services.ConfigureHangfire( _configuration["redis:connectionString"], configuration => { });
	        
            //last
            _applicationContainer = services.CreateAutofacContainer((builder, serviceCollection) =>
            {
	            builder.RegisterModule<WebModule>();
	            builder.RegisterModule<ServicesModule>();
            });

            // Create the IServiceProvider based on the container.
            var provider = new AutofacServiceProvider(_applicationContainer);

            _logger.LogInformation($"{nameof(ConfigureServices)} complete...");

            return provider;
        }

        private JwtSigningKey ConfigureSecurityKey(JwtIssuerOptions issuerOptions)
        {
            var keyString = issuerOptions.Audience;
            var keyBytes = Encoding.Unicode.GetBytes(keyString);
            var signingKey = new JwtSigningKey(keyBytes);
            return signingKey;
        }

        public void Configure(
            IApplicationBuilder app,
            IApplicationLifetime appLifetime,
            IAntiforgery antiforgery,
            IConfiguration configuration)
        {
            _logger.LogInformation($"{nameof(Configure)} starting...");

            //Add custom middleware or use IStartupFilter
            
            appLifetime.ApplicationStopped.Register(() =>
            {
                Log.CloseAndFlush();
                _applicationContainer.Dispose();
            });

            _logger.LogInformation($"{nameof(Configure)} complete...");
        }
    }
}
