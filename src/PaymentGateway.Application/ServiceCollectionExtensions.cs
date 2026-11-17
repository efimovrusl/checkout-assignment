using Microsoft.Extensions.DependencyInjection;

namespace PaymentGateway.Application;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            services.AddScoped<IPaymentsService, PaymentsService>();

            return services;
        }
    }
}