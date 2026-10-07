using CustomerService.Api.ExceptionHandling;
using CustomerService.Api.Services;
using Scalar.AspNetCore;
using CustomerService.Api.Options;
using CustomerService.Api.Data;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.RespectRequiredConstructorParameters = true;
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

string connectionString =
    builder.Configuration.GetConnectionString("CustomerDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'CustomerDatabase' was not found.");

builder.Services.AddDbContext<CustomerDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddOptions<CustomerPolicyOptions>()
    .Bind(builder.Configuration.GetSection(
        CustomerPolicyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddSingleton<ICustomerService, InMemoryCustomerService>();

builder.Services.AddScoped<IAsyncLabService, AsyncLabService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseExceptionHandler();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Customer Service API v1");
    });

    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Customer Service API");
        options.DisableAgent();
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
