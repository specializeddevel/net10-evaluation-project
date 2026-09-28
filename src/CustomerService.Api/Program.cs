using CustomerService.Api.ExceptionHandling;
using CustomerService.Api.Services;
using Scalar.AspNetCore;
using CustomerService.Api.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services
    .AddOptions<CustomerPolicyOptions>()
    .Bind(builder.Configuration.GetSection(
        CustomerPolicyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddSingleton<ICustomerService, InMemoryCustomerService>();

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
