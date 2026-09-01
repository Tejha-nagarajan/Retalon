using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Retalon.Data;
using Serilog;
using Retalon.Extensions;

var builder = WebApplication.CreateBuilder(args);

//Serilog configuration
builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});
// Add services to the container.
builder.Services.AddControllers();
// OpenAPI support
builder.Services.AddOpenApi();
//Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));
// Application services
builder.Services.AddApplicationServices();
// App building
var app = builder.Build();
// Serilog request logging
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Health Check Endpoint
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "Healthy"
    });

})
.WithName("HealthCheck");

// Health Check Endpoint for database

app.MapGet("/health/database", async (ApplicationDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();

        return Results.Ok(new
        {
            status = canConnect ? "Healthy" : "Unhealthy",
            database = canConnect ? "Connected" : "Not Connected"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database connection failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
.WithName("DatabaseHealthCheck");

app.Run();



