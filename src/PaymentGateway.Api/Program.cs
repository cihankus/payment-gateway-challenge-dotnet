using Microsoft.Extensions.Options;

using PaymentGateway.Api.AcquiringBank;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PaymentRequestValidator>();
builder.Services.AddSingleton<IPaymentsRepository, InMemoryPaymentsRepository>();

// Scoped rather than singleton: it depends on a typed HttpClient, which must not be held for the app's lifetime.
builder.Services.AddScoped<PaymentService>();

// Fail at startup rather than on the first payment if the bank settings are missing or invalid.
builder.Services.AddOptions<AcquiringBankOptions>()
    .Bind(builder.Configuration.GetSection(AcquiringBankOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<AcquiringBankOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
