using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quotes.Api.Data;

namespace Quotes.Api.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override JWT settings for predictable test tokens
            var overrides = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "THIS_IS_A_TEST_KEY_32_CHARS_MINIMUM!!",
                ["Jwt:Issuer"] = "quotes-api-tests",
                ["Jwt:Audience"] = "quotes-api-tests"
            };
            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            // Replace Npgsql DbContext with InMemory DbContext for integration tests
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<QuotesDbContext>));
            if (dbContextDescriptor is not null)
                services.Remove(dbContextDescriptor);

            services.AddDbContext<QuotesDbContext>(opt =>
                opt.UseInMemoryDatabase("QuotesDb_Test"));
        });
    }
}
