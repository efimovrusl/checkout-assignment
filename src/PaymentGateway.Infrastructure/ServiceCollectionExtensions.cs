using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using PaymentGateway.Application;
using PaymentGateway.Infrastructure.Bank;

namespace PaymentGateway.Infrastructure;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddInfrastructure(IConfiguration configuration)
        {
            services.AddOptions<BankOptions>()
                .Bind(configuration.GetSection(BankOptions.SectionName))
                .Validate(
                    options => options.BaseUrl is not null && options.TimeoutSeconds > 0,
                    "Bank:BaseUrl must be set and Bank:TimeoutSeconds must be greater than zero.")
                .ValidateOnStart();

            services.AddHttpClient<IBankClient, HttpBankClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<BankOptions>>().Value;
                var baseUrl = options.BaseUrl!.AbsoluteUri;

                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

            services.AddSingleton<IPaymentsRepository, InMemoryPaymentsRepository>();
            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

            return services;
        }
    }
}
