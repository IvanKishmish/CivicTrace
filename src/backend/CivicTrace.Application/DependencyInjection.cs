using CivicTrace.Application.Common.Behaviours;
using CivicTrace.Application.Features.Auth.Register;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace CivicTrace.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();

        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));

        return services;
    }
}