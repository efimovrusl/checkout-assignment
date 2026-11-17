using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PaymentGateway.Application;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplication()
        {
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<ProcessPaymentCommandValidator>();
            services.AddScoped<IPaymentsService, PaymentsService>();

            return services;
        }
    }
}