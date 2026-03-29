using BaaS.Data;
using BaaS.Middleware;
using BaaS.Models;
using BaaS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.Configure<ApiKeySettings>(builder.Configuration.GetSection("ApiKeys"));
builder.Services.AddScoped<ApiKeyAuthService>();
builder.Services.AddScoped<PasswordHashService>();
builder.Services.AddScoped<UserAccountService>();
builder.Services.AddScoped<TableOwnershipService>();
builder.Services.AddScoped<CsvService>();
builder.Services.AddScoped<SchemaDetectionService>();
builder.Services.AddScoped<TableService>();
builder.Services.AddScoped<DataInsertService>();
builder.Services.AddScoped<DynamicDataService>();
builder.Services.AddScoped<ProvisioningService>();
builder.Services.AddSingleton<SwaggerConfigurationService>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Dynamic BaaS API",
        Version = "v1",
        Description = "Dynamic backend APIs for CSV upload, schema detection, runtime tables, CRUD, and API key authentication."
    });

    options.TagActionsBy(api =>
    {
        if (!string.IsNullOrWhiteSpace(api.GroupName))
        {
            return [api.GroupName];
        }

        return [api.ActionDescriptor.RouteValues["controller"] ?? "Default"];
    });

    options.DocInclusionPredicate((_, _) => true);

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Enter your API key. Example: admin123 or readonly123",
        Name = "x-api-key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "ApiKey",
                    Type = ReferenceType.SecurityScheme
                }
            },
            []
        }
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "Dynamic BaaS API Docs";
        options.DisplayRequestDuration();
        options.EnablePersistAuthorization();
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Dynamic BaaS API v1");
    });
}

using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Database initialization failed. API will continue running without database connectivity.");
    }
}

app.UseHttpsRedirection();

app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
