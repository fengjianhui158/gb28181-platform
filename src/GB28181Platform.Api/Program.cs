using System.Threading.Channels;
using GB28181Platform.AiAgent;
using GB28181Platform.AiAgent.Abstractions;
using GB28181Platform.AiAgent.Capabilities.Application;
using GB28181Platform.AiAgent.Capabilities.Plugins;
using GB28181Platform.AiAgent.Capabilities.Persistence;
using GB28181Platform.AiAgent.Prompts;
using GB28181Platform.AiAgent.Runtime;
using GB28181Platform.Api.BackgroundServices;
using GB28181Platform.Api.Hubs;
using GB28181Platform.Application.Streams;
using GB28181Platform.Diagnostic.Browser;
using GB28181Platform.Diagnostic.Browser.VisibleField;
using GB28181Platform.Diagnostic.Engine;
using GB28181Platform.Diagnostic.Steps;
using GB28181Platform.Infrastructure;
using GB28181Platform.Sip;
using GB28181Platform.Sip.Handlers;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// CORS — 开发阶段允许前端跨域
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Controllers
builder.Services.AddControllers();

// SignalR
builder.Services.AddSignalR();

// Infrastructure — SqlSugar + MySQL (启动时自动建表)
var mysqlConn = builder.Configuration.GetConnectionString("MySQL")!;
builder.Services.AddInfrastructure(mysqlConn);

// GB28181 SIP Server
var sipConfig = builder.Configuration.GetSection("SipServer");
builder.Services.AddGb28181Sip(options =>
{
    options.ServerId = sipConfig["ServerId"] ?? options.ServerId;
    options.Realm = sipConfig["Realm"] ?? options.Realm;
    options.Port = int.TryParse(sipConfig["Port"], out var p) ? p : options.Port;
    options.ListenIp = sipConfig["ListenIp"] ?? options.ListenIp;
    options.DefaultPassword = sipConfig["DefaultPassword"] ?? options.DefaultPassword;
    options.KeepaliveTimeoutSeconds = int.TryParse(sipConfig["KeepaliveTimeoutSeconds"], out var kt) ? kt : options.KeepaliveTimeoutSeconds;
    options.KeepaliveCheckIntervalSeconds = int.TryParse(sipConfig["KeepaliveCheckIntervalSeconds"], out var kc) ? kc : options.KeepaliveCheckIntervalSeconds;
});

// Background Services
builder.Services.AddHostedService<SipServerHostedService>();
builder.Services.AddHostedService<DeviceMonitorService>();

// Stream Services
builder.Services.AddSingleton<InviteHandler>();
builder.Services.AddScoped<IStreamAppService, StreamAppService>();

// Diagnostic Engine
builder.Services.AddScoped<IDiagnosticEngine, DiagnosticEngine>();
builder.Services.AddScoped<IDiagnosticStep, PingCheckStep>();
builder.Services.AddScoped<IDiagnosticStep, PortCheckStep>();
builder.Services.AddScoped<IDiagnosticStep, BrowserCheckStep>();
builder.Services.AddSingleton(sp =>
{
    var options = new VisibleFieldAliasOptions();
    builder.Configuration.GetSection("Diagnostic:VisibleFieldAliases").Bind(options.Fields);
    return options;
});
builder.Services.AddSingleton(sp =>
{
    var options = new ManufacturerNavigationOptions();
    builder.Configuration.GetSection("Diagnostic:Manufacturers").Bind(options.Manufacturers);
    return options;
});
builder.Services.AddSingleton(sp =>
{
    var aliasOptions = sp.GetRequiredService<VisibleFieldAliasOptions>();
    return new VisibleFieldConfigExtractor(aliasOptions.Fields);
});
builder.Services.AddSingleton<DahuaRpc2Client>();
builder.Services.AddScoped<CameraBrowserAgent>();

// Diagnostic Task Queue
builder.Services.AddSingleton(Channel.CreateUnbounded<DiagnosticRequest>());
builder.Services.AddHostedService<DiagnosticWorkerService>();

// AI Agent
builder.Services.AddAiAgentCore(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddScoped<IConversationStore, SqlSugarConversationStore>();
builder.Services.AddScoped<IAudioTranscriptionService, OpenAiCompatibleAudioTranscriptionService>();
builder.Services.AddAiAgentPlugin<DeviceCapabilityPlugin>("device");
builder.Services.AddAiAgentPlugin<DiagnosticCapabilityPlugin>("diagnostic");

var app = builder.Build();

app.UseCors();
app.UseSerilogRequestLogging();

app.MapControllers();
app.MapHub<DeviceStatusHub>("/hubs/device-status");

// 首页健康检查
app.MapGet("/", () => Results.Ok(new
{
    service = "GB28181 Platform",
    version = "1.0.0",
    time = DateTime.Now
}));

app.Run();
