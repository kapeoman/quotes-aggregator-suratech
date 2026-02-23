using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Prometheus;

namespace Quotes.Api.Infrastructure.Observability;

public sealed class DependencyFailureMiddleware : IMiddleware
{
    // opcional: métrica para contar caídas de DB
    private static readonly Counter DbUnavailableTotal =
        Metrics.CreateCounter("db_unavailable_total", "Total number of requests that failed due to DB unavailability.");

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (IsDbUnavailable(ex))
        {
            DbUnavailableTotal.Inc();

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers["Retry-After"] = "5";

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Service Unavailable",
                Detail = "Database dependency is unavailable.",
                Type = "https://httpstatuses.com/503"
            };

            // esto es útil para tu test / assessment (código estable)
            var body = new
            {
                code = "DB_UNAVAILABLE",
                message = problem.Detail,
                correlationId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }

    private static bool IsDbUnavailable(Exception ex)
    {
        // Casos típicos cuando Postgres se cae / no resuelve host / socket
        if (ex is NpgsqlException) return true;
        if (ex is PostgresException) return true;
        if (ex is SocketException) return true;
        if (ex is TimeoutException) return true;

        // si viene envuelto (TargetInvocationException, AggregateException, etc.)
        return ex.InnerException is not null && IsDbUnavailable(ex.InnerException);
    }
}