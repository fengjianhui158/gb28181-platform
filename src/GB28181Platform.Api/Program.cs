using GB28181Platform.Api.BackgroundServices;
using GB28181Platform.Api.Hubs;
using GB28181Platform.Infrastructure;
using GB28181Platform.Sip;
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
