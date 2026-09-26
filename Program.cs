using examenParcial.Data;
using examenParcial.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Render indica el puerto en la variable PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection'.");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Redis: variable de entorno ConnectionStrings__Redis. Sin ella, el listado se lee siempre de SQLite.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.ConfigurationOptions = RedisConfiguracion.CrearOpciones(redisConnection);
        options.InstanceName = "examenParcial:";
    });
}
builder.Services.AddScoped<IIncidenciasCacheService, IncidenciasCacheService>();

builder.Services.Configure<AlgoliaOptions>(options =>
{
    options.AppId = builder.Configuration["ALGOLIA_APP_ID"];
    options.SearchApiKey = builder.Configuration["ALGOLIA_SEARCH_API_KEY"];
    options.AdminApiKey = builder.Configuration["ALGOLIA_ADMIN_API_KEY"];
    options.IndexName = builder.Configuration["ALGOLIA_INDEX_NAME"];
});
builder.Services.AddSingleton<IAlgoliaSearchService, AlgoliaSearchService>();

builder.Services.Configure<PieSocketOptions>(options =>
{
    options.ClusterId = PieSocketOptions.NormalizarClusterId(builder.Configuration["PIESOCKET_CLUSTER_ID"]);
    options.ApiKey = builder.Configuration["PIESOCKET_API_KEY"];
    options.ApiSecret = builder.Configuration["PIESOCKET_API_SECRET"];
    options.Channel = builder.Configuration["PIESOCKET_CHANNEL"];
});
builder.Services.AddHttpClient<IPieSocketService, PieSocketService>(client => client.Timeout = TimeSpan.FromSeconds(10));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Render termina TLS en su proxy y reenvía la petición por HTTP.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await SeedData.InicializarAsync(scope.ServiceProvider);
}

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.Run();
