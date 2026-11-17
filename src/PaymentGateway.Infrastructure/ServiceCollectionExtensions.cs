using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Application;

namespace PaymentGateway.Infrastructure;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfrastructure()
        {
            services.AddSingleton<IPaymentsRepository, InMemoryPaymentsRepository>();

            return services;
        }
    }
}