using DAX.EventProcessing;
using DAX.EventProcessing.Dispatcher;
using DAX.EventProcessing.Dispatcher.Topos;
using GraphQL;
using GraphQL.Server.Transports.AspNetCore;
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
using OpenFTTH.APIGateway.DynamicProperties;
using OpenFTTH.APIGateway.GraphQL.Root;
using OpenFTTH.APIGateway.GraphQL.Schematic.Subscriptions;
using OpenFTTH.APIGateway.GraphQL.UtilityNetwork.Subscriptions;
using OpenFTTH.APIGateway.Logging;
using OpenFTTH.APIGateway.Settings;
using OpenFTTH.APIGateway.Util;
using OpenFTTH.APIGateway.Workers;
using OpenFTTH.Address.Business;
using OpenFTTH.Address.Business.Repository;
using OpenFTTH.CQRS;
using OpenFTTH.EventSourcing;
using OpenFTTH.EventSourcing.Postgres;
using OpenFTTH.Events.RouteNetwork;
using OpenFTTH.Events.UtilityNetwork;
using OpenFTTH.RouteNetwork.Business.RouteElements.StateHandling;
using OpenFTTH.Work.Business;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
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

            services.AddGraphQL(b =>
            {
                b.AddSchema<OpenFTTHSchema>();
                b.AddSystemTextJson();
                b.ConfigureExecutionOptions(options =>
                {
                    options.EnableMetrics = false;
                    var logger = options.RequestServices!.GetRequiredService<ILogger<Startup>>();
                    options.UnhandledExceptionDelegate = ctx =>
                    {
                        // We do not care about `OperationCanceledException` since it is not
                        // critical, and happens quite a lot when browsers cancels requests,
                        // resulting in many errors in the logs.
                        if (ctx.OriginalException is OperationCanceledException)
                        {
                            return Task.CompletedTask;
                        }

                        logger.LogError(
                            "{Error} occurred, {StackTrace}",
                            ctx.OriginalException.Message,
                            ctx.OriginalException.StackTrace);

                        return Task.CompletedTask;
                    };
                });
                b.AddDataLoader();
                b.AddGraphTypes(typeof(OpenFTTHSchema).Assembly);
                b.AddWebSocketAuthentication<WebSocketAuthService>();
                b.AddUserContextBuilder(httpContext => new GraphQLUserContext(httpContext));
            });

            // We cannot register subscriptions automatically :-(
            services.AddSingleton<SchematicUpdatedSubscription>();
            services.AddSingleton<TerminalEquipmentConnectivityUpdatedSubscription>();

            // This are needed for WebsocketAuthService
            services.AddHttpClient<WebSocketAuthService>();

            // Auth
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, x =>
                {
                    x.TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidateAudience = true,
                        ValidAudience = configuration.GetSection("Auth").GetValue<string>("Audience"),
                        ValidateIssuer = true,
                        ValidIssuers = configuration.GetSection("Auth").GetValue<string>("ValidIssuers").Split(",").Select(x => x.Trim()),
                        ValidateIssuerSigningKey = true,
                        RequireExpirationTime = true,
                        ValidateLifetime = true,
                        RequireSignedTokens = true,
                    };
                    x.MetadataAddress = $"{configuration.GetSection("Auth").GetValue<string>("Host")}/.well-known/openid-configuration";
                    x.RequireHttpsMetadata = configuration.GetSection("Auth").GetValue<bool>("RequireHttps");
                });

            // Settings
            services.Configure<EventStoreDatabaseSetting>(
                databaseSettings =>
                Configuration.GetSection("EventStoreDatabase").Bind(databaseSettings));

            services.Configure<GeoDatabaseSetting>(
                databaseSettings =>
                Configuration.GetSection("GeoDatabase").Bind(databaseSettings));

            services.Configure<TypesenseSetting>(
                typesenseSettings =>
                Configuration.GetSection("Typesense").Bind(typesenseSettings));

            services.Configure<AuthSetting>(
                authSettings =>
                Configuration.GetSection("Auth").Bind(authSettings));

            services.Configure<NotificationServerSetting>(
                notificationServerSettings =>
                Configuration.GetSection("NotificationServer").Bind(notificationServerSettings));

            services.Configure<OutageServiceSetting>(
                outageServceSettings =>
                Configuration.GetSection("OutageService").Bind(outageServceSettings));

            // Web stuff
            services.AddRazorPages();
            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy(
                    name: AllowedOrigins,
                    builder =>
                    {
                        builder.AllowAnyOrigin();
                        builder.AllowAnyMethod();
                        builder.AllowAnyHeader();
                    });
            });

            services.AddSingleton<IExternalEventProducer>(x =>
            {
                var notificationServerSetting = Configuration
                    .GetSection("NotificationServer")
                    .Get<NotificationServerSetting>() ??
                    throw new InvalidOperationException(
                        $"Could not find service '{nameof(NotificationServerSetting)}'.");

                var ipAddress = Dns.GetHostEntry(notificationServerSetting.Domain).AddressList
                    .First(x => x.AddressFamily == AddressFamily.InterNetwork);

                return new NotificationProducer(
                    ipAddress,
                    notificationServerSetting.Port
                );
            });

            // Event Sourcing and CQRS Stuff
            var assembliesWithBusinessLogic = new Assembly[] {
                AppDomain.CurrentDomain.Load("OpenFTTH.RouteNetwork.Business"),
                AppDomain.CurrentDomain.Load("OpenFTTH.UtilityGraphService.Business"),
                AppDomain.CurrentDomain.Load("OpenFTTH.Schematic.Business"),
                AppDomain.CurrentDomain.Load("OpenFTTH.Work.Business"),
                AppDomain.CurrentDomain.Load("OpenFTTH.Address.Business"),
                AppDomain.CurrentDomain.Load("OpenFTTH.APIGateway")
            };

            // Adds controllers
            services.AddConnections();

            // Setup the event store
            services.AddSingleton<IEventStore>(
                e =>
                new PostgresEventStore(
                    serviceProvider: e.GetRequiredService<IServiceProvider>(),
                    connectionString: e.GetRequiredService<IOptions<EventStoreDatabaseSetting>>().Value.PostgresConnectionString,
                    databaseSchemaName: "events"
                ) as IEventStore
            );

            services.AddProjections(assembliesWithBusinessLogic);

            services.AddCQRS(assembliesWithBusinessLogic);

            services.AddSingleton<WorkContextManager, WorkContextManager>();
            services.AddSingleton<IRouteNetworkState, InMemRouteNetworkState>();
            services.AddSingleton<IRouteNetworkRepository, InMemRouteNetworkRepository>();

            services.AddHostedService<RouteNetworkEventConsumer>();

            // This is disabled in DEBUG to avoid issues of splitting cables when running in debug.
#if !DEBUG
            // Utility network updated
            services.AddHostedService<UtilityNetworkUpdatedEventConsumer>();
#endif

            // Important that it is transient, we want a new instance for each injection.
            services.AddTransient<OpenFTTH.NotificationClient.Client>(x =>
            {
                var notificationServerSetting = Configuration
                    .GetSection("NotificationServer")
                    .Get<NotificationServerSetting>() ??
                    throw new InvalidOperationException(
                        $"Could not find service '{nameof(NotificationServerSetting)}'.");

                var ipAddress = Dns.GetHostEntry(notificationServerSetting.Domain).AddressList
                    .First(x => x.AddressFamily == AddressFamily.InterNetwork);

                return new OpenFTTH.NotificationClient.Client(ipAddress, notificationServerSetting.Port);
            });

            services.AddSingleton<IToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent>, ToposTypedEventObservable<RouteNetworkEditOperationOccuredEvent>>();

            services.AddSingleton<ITypedEventObservable<RouteNetworkElementContainedEquipmentUpdated>, TypedEventObservable<RouteNetworkElementContainedEquipmentUpdated>>();

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
            services.AddSingleton<IAddressRepository>(
                x =>
                new PostgresAddressRepository(
                    x.GetRequiredService<IOptions<GeoDatabaseSetting>>().Value.PostgresConnectionString)
            );

            // Coordinate converter
            services.AddSingleton<UTM32WGS84Converter>();

            // Dynamic properties reader
            services.AddSingleton<DynamicPropertiesClient>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors(AllowedOrigins);

            app.UseAuthentication();

            app.UseGraphQLPlayground();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseWebSockets(new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30)
            });

            app.UseGraphQL<GraphQLHttpMiddlewareWithLogs<OpenFTTHSchema>>(
                "/graphql", new GraphQLHttpMiddlewareOptions
                {
                    AuthorizationRequired = Configuration.GetSection("Auth").GetValue<bool>("Enable")
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
