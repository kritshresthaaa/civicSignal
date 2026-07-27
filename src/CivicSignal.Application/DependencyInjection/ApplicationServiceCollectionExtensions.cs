using CivicSignal.Application.Abstractions.Caching;
using CivicSignal.Application.Abstractions.Geocoding;
using CivicSignal.Application.Abstractions.Messaging;
using CivicSignal.Application.Abstractions.Weather;
using CivicSignal.Application.Agents;
using CivicSignal.Application.AiEvaluations;
using CivicSignal.Application.DataImports;
using CivicSignal.Application.Forecasting;
using CivicSignal.Application.HistoricalComplaints;
using CivicSignal.Application.Incidents;
using CivicSignal.Application.ModelLab;
using CivicSignal.Application.Abstractions.Realtime;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CivicSignal.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceCollectionExtensions).Assembly);
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IIncidentIntelligenceService, IncidentIntelligenceService>();
        services.AddScoped<IControlledTriageAgentService, ControlledTriageAgentService>();
        services.AddScoped<IAiEvaluationService, AiEvaluationService>();
        services.AddScoped<IIncidentForecastingService, IncidentForecastingService>();
        services.AddScoped<IHistoricalComplaintService, HistoricalComplaintService>();
        services.AddScoped<IDataImportJobService, DataImportJobService>();
        services.AddScoped<IModelLabService, ModelLabService>();
        services.AddScoped<IIncidentRealtimeNotifier, NullIncidentRealtimeNotifier>();
        services.TryAddSingleton<IApplicationCache, NullApplicationCache>();
        services.TryAddSingleton<IGeocodingService, NullGeocodingService>();
        services.TryAddSingleton<IWeatherService, NullWeatherService>();
        services.TryAddSingleton<IIncidentProcessingQueue, NullIncidentProcessingQueue>();
        services.TryAddSingleton<IDataImportJobQueue, NullDataImportJobQueue>();

        return services;
    }
}
