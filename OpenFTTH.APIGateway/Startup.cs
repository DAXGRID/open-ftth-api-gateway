﻿using DAX.EventProcessing;
using DAX.EventProcessing.Dispatcher;
using DAX.EventProcessing.Dispatcher.Topos;
using GraphQL.Server;
using GraphQL.Server.Ui.GraphiQL;
using GraphQL.Server.Ui.Playground;
using GraphQL.Server.Ui.Voyager;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using OpenFTTH.APIGateway.Auth;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.GraphQL.Root;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork;
using OpenFTTH.APIGateway.GraphQL.Schematic;
using OpenFTTH.APIGateway.GraphQL.Schematic.Subscriptions;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork;
using OpenFTTH.APIGateway.GraphQL.Work;
using OpenFTTH.APIGateway.Logging;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.APIGateway.Workers;
using OpenFTTH.CQRS;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.EventSourcing;
using OpenFTTH.EventSourcing.Postgres;
using OpenFTTH.RouteNetwork.Business.RouteElements.EventHandling;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using OpenFTTH.Work.Business.InMemTestImpl;
using Serilog;
using System;
using System.Reflection;

namespace OpenFTTH.APIGateway
{
    public class Startup
    {
        private readonly string AllowedOrigins = "_myAllowSpecificOrigins";
        private readonly IWebHostEnvironment _env;
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _env = env;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // To support event deserialization we need setup newtonsoft to this
            JsonConvert.DefaultSettings = (() =>
            {
                var settings = new JsonSerializerSettings();
                settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                settings.Converters.Add(new StringEnumConverter());
                settings.TypeNameHandling = TypeNameHandling.Auto;
                return settings;
            });

            services.AddOptions();

            // Logging
            var configuration = new ConfigurationBuilder()
               .AddJsonFile("appsettings.json", true, false)
               .AddEnvironmentVariables().Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            services.AddLogging(loggingBuilder =>
            {
                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .CreateLogger();

                loggingBuilder.AddSerilog(dispose: true);
            });

            // Auth
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ =>
                {
                    _.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters()
                    {
                        ValidateAudience = true,
                        ValidAudience = "account",
                        ValidateIssuer = true,
                        ValidIssuers = new[] { configuration.GetSection("Auth").GetValue<string>("Host") },
                        ValidateIssuerSigningKey = true,
                        RequireExpirationTime = true,
                        ValidateLifetime = true,
                        RequireSignedTokens = true,
                    };
                    _.MetadataAddress = $"{configuration.GetSection("Auth").GetValue<string>("Host")}/.well-known/openid-configuration";
                    _.RequireHttpsMetadata = configuration.GetSection("Auth").GetValue<bool>("RequireHttps");
                });

            if (_env.IsProduction())
            {
                services.AddGraphQLAuth((settings, provider) => settings.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser()));
            }

            // GraphQL stuff
            services.Configure<KestrelServerOptions>(options => options.AllowSynchronousIO = true);

            services.AddGraphQL((options, provider) =>
            {
                options.EnableMetrics = false;
                var logger = provider.GetRequiredService<ILogger<Startup>>();
                options.UnhandledExceptionDelegate = ctx => logger.LogError("{Error} occured", ctx.OriginalException.Message);
            })
                // Add required services for de/serialization
                .AddSystemTextJson(deserializerSettings => { }, serializerSettings => { }) // For .NET Core 3+
                                                                                           //.AddNewtonsoftJson(deserializerSettings => { }, serializerSettings => { }) // For everything else
                .AddErrorInfoProvider(opt => opt.ExposeExceptionStackTrace = true)
                .AddWebSockets() // Add required services for web socket support
                .AddDataLoader() // Add required services for DataLoader support
                .AddGraphTypes(typeof(Startup)) // Add all IGraphType implementors in assembly which Startup exists
                .AddUserContextBuilder(context => new GraphQLUserContext { User = context.User });

            // Settings
            services.Configure<KafkaSetting>(kafkaSettings =>
                            Configuration.GetSection("Kafka").Bind(kafkaSettings));

            services.Configure<RemoteServicesSetting>(remoteServiceSettings =>
                            Configuration.GetSection("RemoteServices").Bind(remoteServiceSettings));

            services.Configure<EventStoreDatabaseSetting>(databaseSettings =>
                            Configuration.GetSection("EventStoreDatabase").Bind(databaseSettings));

            services.Configure<GeoDatabaseSetting>(databaseSettings =>
                            Configuration.GetSection("GeoDatabase").Bind(databaseSettings));

            // Web stuff
            services.AddRazorPages();

            // GraphQL root schema
            services.AddSingleton<OpenFTTHSchema>();
            services.AddSingleton<OpenFTTHQueries>();
            services.AddSingleton<OpenFTTHMutations>();
            services.AddSingleton<OpenFTTHSubscriptions>();

            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy(name: AllowedOrigins,
                                  builder =>
                                  {
                                      builder.AllowAnyOrigin();
                                      builder.AllowAnyMethod();
                                      builder.AllowAnyHeader();
                                  });
            });

            // Use kafka as external event producer
            services.AddSingleton<IExternalEventProducer>(x =>
                new KafkaProducer(
                    x.GetRequiredService<ILogger<KafkaProducer>>(),
                    x.GetRequiredService<IOptions<KafkaSetting>>().Value.Server,
                    x.GetRequiredService<IOptions<KafkaSetting>>().Value.CertificateFilename
                )
            );

            // Event Sourcing and CQRS Stuff
            var assembliesWithBusinessLogic = new Assembly[] {
                AppDomain.CurrentDomain.Load("OpenFTTH.RouteNetwork.Business"),
                AppDomain.CurrentDomain.Load("OpenFTTH.UtilityGraphService.Business"),
                AppDomain.CurrentDomain.Load("OpenFTTH.Schematic.Business"),
                AppDomain.CurrentDomain.Load("OpenFTTH.Work.Business")
            };

            // Setup the event store
            services.AddSingleton<IEventStore>(e =>
                    new PostgresEventStore(
                        serviceProvider: e.GetRequiredService<IServiceProvider>(),
                        connectionString: e.GetRequiredService<IOptions<EventStoreDatabaseSetting>>().Value.PostgresConnectionString,
                        databaseSchemaName: "events"
                    ) as IEventStore
                );

            services.AddProjections(assembliesWithBusinessLogic);

            services.AddCQRS(assembliesWithBusinessLogic);

            // Core types
            RegisterCoreTypes.Register(services);

            // Work service mockup stuff
            RegisterWorkServiceTypes.Register(services);
            services.AddSingleton<InMemRepoImpl, InMemRepoImpl>();

            // Schematic stuff
            RegisterSchematicTypes.Register(services);

            // Utilty Network stuff
            RegisterUtilityNetworkTypes.Register(services);

            // Route Network stuff
            RegisterRouteNetworkServiceTypes.Register(services);

            services.AddSingleton<RouteNetworkEventHandler, RouteNetworkEventHandler>();
            services.AddSingleton<IRouteNetworkState, InMemRouteNetworkState>();
            services.AddSingleton<IRouteNetworkRepository, InMemRouteNetworkRepository>();
            services.AddSingleton<IToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent>, ToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent>>();

            services.AddHostedService<RouteNetworkEventConsumer>();

            // Utility network updated
            services.AddHostedService<UtilityNetworkUpdatedEventConsumer>();
            services.AddSingleton<IToposTypedEventObservable<RouteNetworkElementContainedEquipmentUpdated>, ToposTypedEventObservable<RouteNetworkElementContainedEquipmentUpdated>>();
            services.AddSingleton<SchematicDiagramObserver>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseCors(AllowedOrigins);

            app.UseAuthentication();

            app.UseWebSockets();

            app.UseGraphQLWebSockets<OpenFTTHSchema>("/graphql");

            app.UseGraphQL<OpenFTTHSchema, GraphQLHttpMiddlewareWithLogs<OpenFTTHSchema>>("/graphql");

            app.UseGraphQLPlayground(new GraphQLPlaygroundOptions
            {
                Path = "/ui/playground",
            });
            app.UseGraphiQLServer(new GraphiQLOptions
            {
                GraphiQLPath = "/ui/graphiql",
                GraphQLEndPoint = "/graphql"
            });
            app.UseGraphQLVoyager(new GraphQLVoyagerOptions
            {
                GraphQLEndPoint = "/graphql",
                Path = "/ui/voyager"
            });
        }
    }
}


