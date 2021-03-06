﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApp2_0.Middleware;
using Microsoft.AspNetCore.Http;
using WebApp2_0.Services;
using WebApp2_0.Models;
using Microsoft.AspNetCore.Routing;
using System.Diagnostics;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.FileProviders;
using System.Reflection;
using System.IO;
using Microsoft.Extensions.Logging;
using WebApp2_0.Logging;
using System.Globalization;

namespace WebApp2_0
{
    public class Startup
    {
        private IHostingEnvironment _hostingEnvironment;
        private ILogger _logger;
        private ILogger _logger2;

        public Startup(IConfiguration configuration, IHostingEnvironment env, ILogger<Startup> logger, ILoggerFactory loggerFactory)
        {
            Configuration = configuration;
            _hostingEnvironment = env;
            _logger = logger;
            _logger2 = loggerFactory.CreateLogger("CustomCategory");
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                .AddSessionStateTempDataProvider();  //TempData to session state.

            //services.AddScoped<IDataService, DataService>();
            //services.AddTransient<IDataService, DataService>();
            //services.AddSingleton<IDataService, DataService>();
            services.AddScoped<IDataService>(sp => new DataService());

            //Get configuration setting setup
            services.Configure<DataSettings>(Configuration.GetSection("DataSettings"));

            // routing
            services.AddRouting();

            //file providers
            var physicalProvider = _hostingEnvironment.ContentRootFileProvider;
            var embeddedProvider = new EmbeddedFileProvider(Assembly.GetEntryAssembly());
            var compositeProvider = new CompositeFileProvider(physicalProvider, embeddedProvider);

            // choose one provider to use for the app and register it
            //services.AddSingleton<IFileProvider>(physicalProvider);
            //services.AddSingleton<IFileProvider>(embeddedProvider);
            services.AddSingleton<IFileProvider>(compositeProvider);

            //Sesssion
            services.AddDistributedMemoryCache();

            // Session and TempData
            services.AddSession();

            services.AddLocalization(options => options.ResourcesPath = "Resources");
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            var configuration = app.ApplicationServices.GetService<TelemetryConfiguration>();
            configuration.DisableTelemetry = true;

            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else {
                app.UseExceptionHandler("/Home/Error");
            }

            var supportedCultures = new[] {
                new CultureInfo("en"),
                new CultureInfo("fr")
            };

            app.UseRequestLocalization(new RequestLocalizationOptions {
                DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(supportedCultures[0]),
                SupportedCultures = supportedCultures,
                SupportedUICultures = supportedCultures
            });

            app.UseStaticFiles();

            app.UseStaticFiles(new StaticFileOptions() {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Files/images")),
                RequestPath = new PathString("/StaticFiles")
            });

            // Session and TempData
            app.UseSession();

            app.UseEncodeUri();

            // Show custom middleware inline
            app.Use(async (context, next) => {

                // Httpcontext.items 
                context.Items["MyContextItemName"] = "My context item name";

                await next();

                string redirect = context.Response.Headers["X-Redirect"];
                if (!string.IsNullOrWhiteSpace(redirect)) {
                    //context.Response.Headers.Add("X-nonsense", "pure nonsense"); //will cause and issue
                    Debug.WriteLine($"***** X-Redirect found value: {redirect}");
                    _logger.LogDebug($"***** Debug: X-Redirect found value: {redirect}");
                    _logger2.LogDebug($"***** CustomCat: X-Redirect found value: {redirect}");
                    _logger.ViewRequested();
                    _logger.ViewRequestedOptions("{ key1=value1 }", 20);
                }
            });

            app.UseWhen(context => context.Request.Path.StartsWithSegments("/Home/Contact"), (app2) => {
                app2.Use(async (context, next) => {

                    Debug.WriteLine($"***** Contact Page");
                    await next();
                });
            });

            app.Map("/Home/About", (app2) => {
                app2.Use(async (context, next) => {

                    Debug.WriteLine($"***** About Page");
                    await next();
                });
            });

            app.UseMvc(routes => {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseStatusCodePages(async context => {
                context.HttpContext.Response.ContentType = "text/plain";
                await context.HttpContext.Response.WriteAsync(
                    "Status code page, status code: " +
                    context.HttpContext.Response.StatusCode);
            });

            //custom routing
            app.UseRouter(routes => {
                routes.MapGet("test/{id:int}", context => {
                    var id = context.GetRouteValue("id");
                    return context.Response.WriteAsync($"Hi, number: {id}");
                });
                routes.MapGet("test/{id:alpha}", context => {
                    var id = context.GetRouteValue("id");
                    return context.Response.WriteAsync($"Hi, string: {id}");
                });
                routes.MapGet("test/{*slug}", context => {
                    var id = context.GetRouteValue("id");
                    return context.Response.WriteAsync("Slugs!");
                });
            });

            // Basic middleware showing Run statement
            //app.Use(async (context, next) => {

            //    Console.Write("Why am I here?\n");

            //    await next.Invoke();

            //    Console.Write("After here?\n");

            //    //Don't write to the response after calling the next delegate

            //});

            //app.Run(async (context) => {
            //    Console.Write("Hello ILM!\n");
            //    await context.Response.WriteAsync("<p>Hello ILM!</p>");
            //});
        }
    }
}
