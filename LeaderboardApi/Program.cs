using LeaderboardService;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = null;
    options.Limits.MaxConcurrentUpgradedConnections = null;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

// Add services to the container.
//builder.Services.AddSingleton<ILeaderboardService, LeaderboardServiceImpl>();
builder.Services.AddSingleton<ILeaderboardService, LeaderboardServiceV3Impl>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
