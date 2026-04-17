using System.Reflection;
using Chronos.Data.Context;
using Chronos.Data.Repositories.Auth;
using Chronos.Data.Repositories.Management;
using Chronos.Data.Repositories.Resources;
using Chronos.Data.Repositories.Schedule;
using Chronos.Domain.Management;
using Chronos.Offboarding;
using Chronos.Offboarding.Removers;
using Chronos.Offboarding.Workers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronos.Tests.Offboarding.Workers;

[TestFixture]
[Category("Unit")]
public class OffboardingWorkerTests
{
    private IOrganizationRepository _organizationRepository = null!;
    private IDepartmentRepository _departmentRepository = null!;
    private IAssignmentRepository _assignmentRepository = null!;
    private IServiceScopeFactory _scopeFactory = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _departmentRepository = Substitute.For<IDepartmentRepository>();
        _assignmentRepository = Substitute.For<IAssignmentRepository>();

        var services = new ServiceCollection();

        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        services.AddSingleton(_organizationRepository);
        services.AddSingleton(_departmentRepository);
        services.AddSingleton(_assignmentRepository);
        services.AddSingleton(Substitute.For<IActivityConstraintRepository>());
        services.AddSingleton(Substitute.For<IUserPreferenceRepository>());
        services.AddSingleton(Substitute.For<IUserConstraintRepository>());
        services.AddSingleton(Substitute.For<IResourceAttributeAssignmentRepository>());
        services.AddSingleton(Substitute.For<IActivityRepository>());
        services.AddSingleton(Substitute.For<ISlotRepository>());
        services.AddSingleton(Substitute.For<ISubjectRepository>());
        services.AddSingleton(Substitute.For<ISchedulingPeriodRepository>());
        services.AddSingleton(Substitute.For<IResourceRepository>());
        services.AddSingleton(Substitute.For<IResourceAttributeRepository>());
        services.AddSingleton(Substitute.For<IResourceTypeRepository>());
        services.AddSingleton(Substitute.For<IRoleAssignmentRepository>());
        services.AddSingleton(Substitute.For<IOrganizationPolicyRepository>());
        services.AddSingleton(Substitute.For<IUserRepository>());
        services.AddSingleton(Substitute.For<ILogger<OrganizationRemover>>());
        services.AddSingleton(Substitute.For<ILogger<DepartmentRemover>>());

        services.AddScoped<OrganizationRemover>();
        services.AddScoped<DepartmentRemover>();

        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    }

    private OffboardingWorker CreateWorker(int retentionDays = 30)
    {
        var config = new OffboardingConfiguration
        {
            CronSchedule = "* * * * *",
            RetentionDays = retentionDays,
        };
        return new OffboardingWorker(
            _scopeFactory,
            Options.Create(config),
            Substitute.For<ILogger<OffboardingWorker>>()
        );
    }

    /// <summary>
    /// Invokes the private RunOffboardingAsync directly to bypass the cron delay.
    /// </summary>
    private static Task InvokeRunOffboardingAsync(OffboardingWorker worker, int retentionDays, CancellationToken ct)
    {
        var method = typeof(OffboardingWorker)
            .GetMethod("RunOffboardingAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(worker, [retentionDays, ct])!;
    }

    [Test]
    public async Task GivenExpiredOrganization_WhenRunOffboarding_ThenRemovesIt()
    {
        var orgId = Guid.NewGuid();
        _organizationRepository.GetAllDeletedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Organization>
            {
                new() { Id = orgId, Name = "Expired", Deleted = true, DeletedTime = DateTime.UtcNow.AddDays(-60) },
            });
        _departmentRepository.GetAllDeletedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Department>());

        var worker = CreateWorker(retentionDays: 30);
        await InvokeRunOffboardingAsync(worker, 30, CancellationToken.None);

        await _organizationRepository.Received(1).GetByIdAsync(orgId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GivenOrganizationWithinRetention_WhenRunOffboarding_ThenSkipsIt()
    {
        var orgId = Guid.NewGuid();
        _organizationRepository.GetAllDeletedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Organization>
            {
                new() { Id = orgId, Name = "Recent", Deleted = true, DeletedTime = DateTime.UtcNow.AddDays(-5) },
            });
        _departmentRepository.GetAllDeletedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Department>());

        var worker = CreateWorker(retentionDays: 30);
        await InvokeRunOffboardingAsync(worker, 30, CancellationToken.None);

        await _assignmentRepository.DidNotReceive()
            .DeleteAllByOrganizationIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GivenExpiredDepartment_WhenRunOffboarding_ThenRemovesIt()
    {
        var deptId = Guid.NewGuid();
        _organizationRepository.GetAllDeletedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Organization>());
        _departmentRepository.GetAllDeletedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Department>
            {
                new() { Id = deptId, OrganizationId = Guid.NewGuid(), Name = "OldDept", Deleted = true, DeletedTime = DateTime.UtcNow.AddDays(-60) },
            });

        var worker = CreateWorker(retentionDays: 30);
        await InvokeRunOffboardingAsync(worker, 30, CancellationToken.None);

        await _departmentRepository.Received(1).GetByIdAsync(deptId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GivenOneOrgRemovalFails_WhenRunOffboarding_ThenContinuesWithNext()
    {
        var failOrg = new Organization
        {
            Id = Guid.NewGuid(), Name = "FailOrg", Deleted = true, DeletedTime = DateTime.UtcNow.AddDays(-60),
        };
        var okOrg = new Organization
        {
            Id = Guid.NewGuid(), Name = "OkOrg", Deleted = true, DeletedTime = DateTime.UtcNow.AddDays(-60),
        };

        _organizationRepository.GetAllDeletedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Organization> { failOrg, okOrg });
        _departmentRepository.GetAllDeletedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Department>());

        // Make the first org's assignment delete throw
        _assignmentRepository.DeleteAllByOrganizationIdAsync(failOrg.Id, Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new Exception("Connection lost"));
        _assignmentRepository.DeleteAllByOrganizationIdAsync(okOrg.Id, Arg.Any<CancellationToken>())
            .Returns(0);

        var worker = CreateWorker(retentionDays: 30);
        await InvokeRunOffboardingAsync(worker, 30, CancellationToken.None);

        // The second org should still be attempted
        await _assignmentRepository.Received(1)
            .DeleteAllByOrganizationIdAsync(okOrg.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GivenMixOfExpiredAndRecent_WhenRunOffboarding_ThenOnlyRemovesExpired()
    {
        var expiredOrg = new Organization
        {
            Id = Guid.NewGuid(), Name = "Expired", Deleted = true, DeletedTime = DateTime.UtcNow.AddDays(-45),
        };
        var recentOrg = new Organization
        {
            Id = Guid.NewGuid(), Name = "Recent", Deleted = true, DeletedTime = DateTime.UtcNow.AddDays(-10),
        };

        _organizationRepository.GetAllDeletedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Organization> { expiredOrg, recentOrg });
        _departmentRepository.GetAllDeletedAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Department>());

        var worker = CreateWorker(retentionDays: 30);
        await InvokeRunOffboardingAsync(worker, 30, CancellationToken.None);

        await _assignmentRepository.Received(1)
            .DeleteAllByOrganizationIdAsync(expiredOrg.Id, Arg.Any<CancellationToken>());
        await _assignmentRepository.DidNotReceive()
            .DeleteAllByOrganizationIdAsync(recentOrg.Id, Arg.Any<CancellationToken>());
    }
}
