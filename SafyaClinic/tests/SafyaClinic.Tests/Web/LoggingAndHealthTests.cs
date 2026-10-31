using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SafyaClinic.Infrastructure.Data;
using SafyaClinic.Web.Startup;
using Xunit;

namespace SafyaClinic.Tests.Web;

/// <summary>F3a: user id in the log scope; database health check.</summary>
public class LoggingAndHealthTests
{
    private sealed class ScopeRecorder : ILogger<UserLogScopeMiddleware>
    {
        public List<object> Scopes { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull { Scopes.Add(state); return null; }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }
    }

    [Fact]
    public async Task Signed_in_requests_log_the_user_id()
    {
        var recorder = new ScopeRecorder();
        var middleware = new UserLogScopeMiddleware(_ => Task.CompletedTask, recorder);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "test"))
        };

        await middleware.InvokeAsync(context);

        var scope = Assert.IsAssignableFrom<IDictionary<string, object>>(Assert.Single(recorder.Scopes));
        Assert.Equal("7", scope["UserId"]);
    }

    [Fact]
    public async Task Anonymous_requests_add_no_user_scope()
    {
        var recorder = new ScopeRecorder();
        var middleware = new UserLogScopeMiddleware(_ => Task.CompletedTask, recorder);

        await middleware.InvokeAsync(new DefaultHttpContext());

        Assert.Empty(recorder.Scopes);
    }

    [Fact]
    public async Task Reachable_database_is_healthy()
    {
        await using var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseInMemoryDatabase($"health-{Guid.NewGuid()}").Options);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Unreachable_database_is_unhealthy()
    {
        // Port 1 on this machine: nothing listens, so the connection is refused quickly.
        await using var db = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
            .UseSqlServer("Server=tcp:127.0.0.1,1;Database=HealthCheckTest;Integrated Security=True;Connect Timeout=2;TrustServerCertificate=True")
            .Options);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}