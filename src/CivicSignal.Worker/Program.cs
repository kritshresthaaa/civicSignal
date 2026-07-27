using CivicSignal.Application.DependencyInjection;
using CivicSignal.Infrastructure.DependencyInjection;
using CivicSignal.Worker;
using CivicSignal.Worker.DataImports;
using CivicSignal.Worker.Messaging;
using CivicSignal.Worker.Options;
using CivicSignal.Worker.Processing;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<IncidentProcessingWorkerOptions>(
    builder.Configuration.GetSection(IncidentProcessingWorkerOptions.SectionName));
builder.Services.Configure<DataImportWorkerOptions>(
    builder.Configuration.GetSection(DataImportWorkerOptions.SectionName));
builder.Services.AddScoped<IncidentProcessingPipeline>();
builder.Services.AddHostedService<IncidentProcessingWorker>();
builder.Services.AddHostedService<RabbitMqIncidentProcessingConsumer>();
builder.Services.AddHostedService<DataImportJobWorker>();
builder.Services.AddHostedService<RabbitMqDataImportJobConsumer>();

var host = builder.Build();
host.Run();
