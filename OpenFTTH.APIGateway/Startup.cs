using DAX.EventProcessing;
using DAX.EventProcessing.Dispatcher;
using DAX.EventProcessing.Dispatcher.Topos;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Server;
using GraphQL.Server.Transports.Subscriptions.Abstractions;
using GraphQL.SystemReactive;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using OpenFTTH.APIGateway.Auth;
using OpenFTTH.APIGateway.CoreTypes;
using OpenFTTH.APIGateway.GraphQL.Addresses;
using OpenFTTH.APIGateway.GraphQL.Root;
using OpenFTTH.APIGateway.GraphQL.RouteNetwork;
using OpenFTTH.APIGateway.GraphQL.Schematic;
using OpenFTTH.APIGateway.GraphQL.Schematic.Subscriptions;
using OpenFTTH.APIGateway.GraphQL.Search;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Subscriptions;
using OpenFTTH.APIGateway.GraphQL.Work;
using OpenFTTH.APIGateway.Logging;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.APIGateway.Workers;
using OpenFTTH.Address.Business;
using OpenFTTH.Address.Business.Repository;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.EventSourcing.Postgres;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.Business.RouteElements.EventHandling;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using OpenFTTH.Work.Business.InMemTestImpl;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System;
using System.Collections.Generic;
using MicrosoftDI = GraphQL.MicrosoftDI;
using System.Reflection;
using Typesense.Setup;

namespace OpenFTTH.APIGateway
{
    public class Startup
    {
        private readonly string AllowedOrigins = "_myAllowSpecificOrigins";
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, false)
                .AddEnvironmentVariables().Build();

            ConfigureLogging(configuration, services);

            ConfigureSerialization();

            // Auth
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ =>
                {
                    _.TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidateAudience = true,
                        ValidAudience = configuration.GetSection("Auth").GetValue<string>("Audience"),
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

            services.AddHttpContextAccessor();
            services.AddTransient<IOperationMessageListener, AuthenticationListener>();
            services.AddHttpClient<IOperationMessageListener, AuthenticationListener>();

            if (configuration.GetSection("Auth").GetValue<bool>("Enable"))
            {
                services.AddGraphQLAuth((settings, provider) => settings.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser()));
            }

            MicrosoftDI.GraphQLBuilderExtensions.AddGraphQL(services)
                .AddSubscriptionDocumentExecuter()
                .AddServer(true)
                .AddSchema<OpenFTTHSchema>()
                .ConfigureExecution(options =>
                {
                    options.EnableMetrics = false;
                    var logger = options.RequestServices.GetRequiredService<ILogger<Startup>>();
                    options.UnhandledExceptionDelegate = ctx => logger.LogWarning("Unhandled Exception GraphQL: {Error} occurred", ctx.OriginalException.Message);
                })
                .AddSystemTextJson()
                .AddWebSockets()
                .AddDataLoader()
                .AddGraphTypes(typeof(OpenFTTHSchema).Assembly)
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

            services.Configure<TypesenseSetting>(typesenseSettings =>
                     Configuration.GetSection("Typesense").Bind(typesenseSettings));

            services.Configure<AuthSetting>(authSettings =>
                            Configuration.GetSection("Auth").Bind(authSettings));

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
                AppDomain.CurrentDomain.Load("OpenFTTH.Work.Business"),
                AppDomain.CurrentDomain.Load("OpenFTTH.Address.Business")
            };

            // Adds controllers
            services.AddConnections();

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

            // Address stuff
            RegisterAddressTypes.Register(services);

            // Search stuff
            RegisterSearchServiceTypes.Register(services);

            services.AddSingleton<RouteNetworkEventHandler, RouteNetworkEventHandler>();
            services.AddSingleton<IRouteNetworkState, InMemRouteNetworkState>();
            services.AddSingleton<IRouteNetworkRepository, InMemRouteNetworkRepository>();
            services.AddSingleton<IToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent>, ToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent>>();

            services.AddHostedService<RouteNetworkEventConsumer>();

            // Utility network updated
            services.AddHostedService<UtilityNetworkUpdatedEventConsumer>();
            services.AddSingleton<IToposTypedEventObservable<RouteNetworkElementContainedEquipmentUpdated>, ToposTypedEventObservable<RouteNetworkElementContainedEquipmentUpdated>>();
            services.AddSingleton<SchematicDiagramObserver>();
            services.AddSingleton<TerminalEquipmentConnectivityObserver>();

            // Typesense
            services.AddTypesenseClient(config =>
            {
                var typesenseSetting = Configuration.GetSection("Typesense").Get<TypesenseSetting>();

                config.ApiKey = typesenseSetting.ApiKey;
                config.Nodes = new List<Node>
                {
                    new Node(typesenseSetting.Host, typesenseSetting.Port, "http")
                };
            });

            // Address service
            services.AddSingleton<IAddressRepository>(x =>
                    new PostgresAddressRepository(x.GetRequiredService<IOptions<GeoDatabaseSetting>>().Value.PostgresConnectionString)
            );
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseCors(AllowedOrigins);

            app.UseAuthentication();

            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            });

            app.UseGraphQLWebSockets<OpenFTTHSchema>();
            app.UseGraphQL<OpenFTTHSchema, GraphQLHttpMiddlewareWithLogs<OpenFTTHSchema>>();
            app.UseGraphQLPlayground();
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private static void ConfigureLogging(IConfigurationRoot configuration, IServiceCollection services)
        {
            services.AddLogging(loggingBuilder =>
            {
                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new CompactJsonFormatter())
                    .CreateLogger();

                loggingBuilder.ClearProviders().AddSerilog(logger, true);
            });
        }

        private static void ConfigureSerialization()
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
        }
    }
}
