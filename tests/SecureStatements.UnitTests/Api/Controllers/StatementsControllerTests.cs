using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SecureStatements.Api.Contracts;
using SecureStatements.Api.Controllers;
using SecureStatements.Application.Abstractions;
using SecureStatements.Application.Options;
using SecureStatements.Application.Services;
using SecureStatements.Domain;
using SecureStatements.UnitTests.Fakes;

namespace SecureStatements.UnitTests.Api.Controllers;

public sealed class StatementsControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static DownloadTokenService Tokens(FixedClock clock) =>
        new(Options.Create(new DownloadTokenOptions
        {
            SigningKey = new string('k', 32),
            LifetimeMinutes = 15
        }), clock);

    private static Statement StatementFor(string customerId, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        CustomerId = customerId,
        Period = "2026-07",
        FileName = "s.pdf",
        BlobKey = Guid.NewGuid().ToString("N"),
        SizeBytes = 3,
        CreatedAt = Now
    };

    private static StatementsController Build(
        FakeStatementRepository repo,
        RecordingAuditRepository audit,
        ICurrentUser currentUser,
        FixedClock clock)
    {
        var statements = new StatementService(repo, new FakeBlobStore(), clock);
        var auditService = new AuditService(audit, clock);
        var controller = new StatementsController(
            statements, Tokens(clock), auditService, currentUser)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("api.test");

        // Url.Action is used to build the absolute link; a tiny stub keeps this a unit test.
        controller.Url = new StubUrlHelper(controller.ControllerContext);
        return controller;
    }

    [Fact]
    public async Task List_noCustomerId_returnsForbid()
    {
        var controller = Build(
            new FakeStatementRepository(),
            new RecordingAuditRepository(),
            new StubCurrentUser { IsAuthenticated = true, CustomerId = null },
            new FixedClock(Now));

        var result = await controller.List(CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task List_returnsOnlyCallersStatements_asSafeSummaries()
    {
        var mine = StatementFor("cust-1");
        var theirs = StatementFor("cust-2");
        var repo = new FakeStatementRepository().Seed(mine).Seed(theirs);

        var controller = Build(
            repo,
            new RecordingAuditRepository(),
            new StubCurrentUser { IsAuthenticated = true, CustomerId = "cust-1" },
            new FixedClock(Now));

        var result = await controller.List(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IReadOnlyList<StatementSummaryDto>>().Subject;
        items.Should().ContainSingle().Which.Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task CreateDownloadLink_ownedStatement_returnsLink_andAuditsIssued()
    {
        var mine = StatementFor("cust-1");
        var repo = new FakeStatementRepository().Seed(mine);
        var audit = new RecordingAuditRepository();

        var controller = Build(
            repo, audit,
            new StubCurrentUser { IsAuthenticated = true, CustomerId = "cust-1", ClientIp = "9.9.9.9" },
            new FixedClock(Now));

        var result = await controller.CreateDownloadLink(mine.Id, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var link = ok.Value.Should().BeOfType<DownloadLinkDto>().Subject;
        link.DownloadUrl.Should().Contain("/download/");
        link.ExpiresAt.Should().Be(Now.AddMinutes(15));
        audit.Entries.Should().ContainSingle().Which.Action.Should().Be(AuditActions.LinkIssued);
    }

    [Fact]
    public async Task CreateDownloadLink_notOwned_returnsNotFound_andAuditsDenied()
    {
        var theirs = StatementFor("cust-2");
        var repo = new FakeStatementRepository().Seed(theirs);
        var audit = new RecordingAuditRepository();

        var controller = Build(
            repo, audit,
            new StubCurrentUser { IsAuthenticated = true, CustomerId = "cust-1" },
            new FixedClock(Now));

        var result = await controller.CreateDownloadLink(theirs.Id, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
        audit.Entries.Should().ContainSingle().Which.Action.Should().Be(AuditActions.LinkDenied);
    }

    [Fact]
    public async Task CreateDownloadLink_noCustomerId_returnsForbid()
    {
        var controller = Build(
            new FakeStatementRepository(),
            new RecordingAuditRepository(),
            new StubCurrentUser { IsAuthenticated = false, CustomerId = null },
            new FixedClock(Now));

        var result = await controller.CreateDownloadLink(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    // Minimal IUrlHelper that produces a plausible absolute URL, so the controller's link
    // building can be exercised without spinning up routing/a web host.
    private sealed class StubUrlHelper : IUrlHelper
    {
        public StubUrlHelper(ActionContext actionContext) => ActionContext = actionContext;

        public ActionContext ActionContext { get; }

        public string? Action(UrlActionContext actionContext)
        {
            var token = actionContext.Values is null
                ? "t"
                : new RouteValueDictionary(actionContext.Values)["token"];
            return $"https://api.test/download/{token}";
        }

        public string? Content(string? contentPath) => contentPath;

        public bool IsLocalUrl(string? url) => true;

        public string? Link(string? routeName, object? values) => "https://api.test/";

        public string? RouteUrl(UrlRouteContext routeContext) => "https://api.test/";
    }
}


