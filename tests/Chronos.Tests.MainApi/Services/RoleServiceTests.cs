using Chronos.Data.Repositories.Management;
using Chronos.Domain.Management;
using Chronos.Domain.Management.Roles;
using Chronos.MainApi.Auth.Contracts;
using Chronos.MainApi.Management.Services;
using Chronos.MainApi.Management.Services.External;
using Chronos.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronos.Tests.MainApi.Services;

[TestFixture]
[Category("Unit")]
public class RoleServiceTests
{
    private IRoleAssignmentRepository _roleAssignmentRepository = null!;
    private IAuthClient _authClient = null!;
    private ManagementValidationService _validationService = null!;
    private ILogger<RoleService> _logger = null!;
    private RoleService _roleService = null!;
    private IOrganizationRepository _organizationRepository = null!;
    private IDepartmentRepository _departmentRepository = null!;
    private ILogger<ManagementValidationService> _validationLogger = null!;

    [SetUp]
    public void SetUp()
    {
        _roleAssignmentRepository = Substitute.For<IRoleAssignmentRepository>();
        _authClient = Substitute.For<IAuthClient>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _departmentRepository = Substitute.For<IDepartmentRepository>();
        _validationLogger = Substitute.For<ILogger<ManagementValidationService>>();
        _logger = Substitute.For<ILogger<RoleService>>();
        
        _validationService = new ManagementValidationService(
            _organizationRepository,
            _departmentRepository,
            _validationLogger);
        
        _roleService = new RoleService(
            _roleAssignmentRepository,
            _authClient,
            _validationService,
            _logger);
    }

    [Test]
    public async Task AddAssignmentAsync_WithIsSystemAssignedTrue_CreatesSystemAssignedRole()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var role = Role.Administrator;
        var isSystemAssigned = true;

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        
        _roleAssignmentRepository.AddAsync(Arg.Any<RoleAssignment>())
            .Returns(callInfo =>
            {
                var assignment = callInfo.Arg<RoleAssignment>();
                return Task.FromResult(assignment);
            });

        var result = await _roleService.AddAssignmentAsync(organizationId, null, userId, role, isSystemAssigned);

        await _roleAssignmentRepository.Received(1).AddAsync(
            Arg.Is<RoleAssignment>(ra =>
                ra.OrganizationId == organizationId &&
                ra.UserId == userId &&
                ra.Role == role &&
                ra.IsSystemAssigned == true &&
                ra.DepartmentId == null));
    }

    [Test]
    public async Task AddAssignmentAsync_WithIsSystemAssignedFalse_CreatesUserAssignedRole()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var role = Role.Viewer;
        var isSystemAssigned = false;

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        
        _roleAssignmentRepository.AddAsync(Arg.Any<RoleAssignment>())
            .Returns(callInfo =>
            {
                var assignment = callInfo.Arg<RoleAssignment>();
                return Task.FromResult(assignment);
            });

        var result = await _roleService.AddAssignmentAsync(organizationId, null, userId, role, isSystemAssigned);

        await _roleAssignmentRepository.Received(1).AddAsync(
            Arg.Is<RoleAssignment>(ra =>
                ra.OrganizationId == organizationId &&
                ra.UserId == userId &&
                ra.Role == role &&
                ra.IsSystemAssigned == false &&
                ra.DepartmentId == null));
    }

    [Test]
    public async Task AddAssignmentAsync_WithoutIsSystemAssignedParameter_DefaultsToFalse()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var role = Role.ResourceManager;

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        
        _roleAssignmentRepository.AddAsync(Arg.Any<RoleAssignment>())
            .Returns(callInfo =>
            {
                var assignment = callInfo.Arg<RoleAssignment>();
                return Task.FromResult(assignment);
            });

        var result = await _roleService.AddAssignmentAsync(organizationId, null, userId, role);

        await _roleAssignmentRepository.Received(1).AddAsync(
            Arg.Is<RoleAssignment>(ra =>
                ra.IsSystemAssigned == false));
    }

    [Test]
    public async Task AddAssignmentAsync_WithDepartmentId_ValidatesDepartment()
    {
        var organizationId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var role = Role.Operator;

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _departmentRepository.GetByIdAsync(departmentId)
            .Returns(new Department { Id = departmentId, OrganizationId = organizationId, Deleted = false });
        
        _roleAssignmentRepository.AddAsync(Arg.Any<RoleAssignment>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<RoleAssignment>()));

        var result = await _roleService.AddAssignmentAsync(organizationId, departmentId, userId, role);

        await _departmentRepository.Received(1).GetByIdAsync(departmentId);
    }

    [Test]
    public async Task RemoveAssignmentAsync_WithSystemAssignedRole_ThrowsBadRequestException()
    {
        var organizationId = Guid.NewGuid();
        var roleAssignmentId = Guid.NewGuid();
        var systemAssignedRole = new RoleAssignment
        {
            Id = roleAssignmentId,
            OrganizationId = organizationId,
            UserId = Guid.NewGuid(),
            Role = Role.Administrator,
            IsSystemAssigned = true
        };

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _roleAssignmentRepository.GetAsync(organizationId, roleAssignmentId)
            .Returns(Task.FromResult(systemAssignedRole)!);

        var ex = Assert.ThrowsAsync<BadRequestException>(async () =>
            await _roleService.RemoveAssignmentAsync(organizationId, roleAssignmentId));

        Assert.That(ex!.Message, Is.EqualTo("Cannot remove system-assigned role assignments"));
        
        await _roleAssignmentRepository.DidNotReceive().DeleteAsync(organizationId, roleAssignmentId);
    }

    [Test]
    public async Task RemoveAssignmentAsync_WithUserAssignedRole_DeletesSuccessfully()
    {
        var organizationId = Guid.NewGuid();
        var roleAssignmentId = Guid.NewGuid();
        var userAssignedRole = new RoleAssignment
        {
            Id = roleAssignmentId,
            OrganizationId = organizationId,
            UserId = Guid.NewGuid(),
            Role = Role.Viewer,
            IsSystemAssigned = false
        };

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _roleAssignmentRepository.GetAsync(organizationId, roleAssignmentId)
            .Returns(Task.FromResult(userAssignedRole)!);
        _roleAssignmentRepository.DeleteAsync(organizationId, roleAssignmentId)
            .Returns(Task.CompletedTask);

        await _roleService.RemoveAssignmentAsync(organizationId, roleAssignmentId);

        await _roleAssignmentRepository.Received(1).DeleteAsync(organizationId, roleAssignmentId);
    }

    [Test]
    public async Task RemoveAssignmentAsync_WithNonExistentRole_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var roleAssignmentId = Guid.NewGuid();

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _roleAssignmentRepository.GetAsync(organizationId, roleAssignmentId)
            .ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _roleService.RemoveAssignmentAsync(organizationId, roleAssignmentId));

        Assert.That(ex!.Message, Is.EqualTo("Role assignment not found"));
        
        await _roleAssignmentRepository.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Test]
    public async Task RemoveAssignmentAsync_ValidatesOrganization()
    {
        var organizationId = Guid.NewGuid();
        var roleAssignmentId = Guid.NewGuid();
        var userAssignedRole = new RoleAssignment
        {
            Id = roleAssignmentId,
            OrganizationId = organizationId,
            UserId = Guid.NewGuid(),
            Role = Role.UserManager,
            IsSystemAssigned = false
        };

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _roleAssignmentRepository.GetAsync(organizationId, roleAssignmentId)
            .Returns(Task.FromResult(userAssignedRole)!);
        _roleAssignmentRepository.DeleteAsync(organizationId, roleAssignmentId)
            .Returns(Task.CompletedTask);

        await _roleService.RemoveAssignmentAsync(organizationId, roleAssignmentId);

        await _organizationRepository.Received(1).GetByIdAsync(organizationId);
    }

    [Test]
    public async Task GetAssignmentAsync_WithExistingAssignment_ReturnsAssignment()
    {
        var organizationId = Guid.NewGuid();
        var roleAssignmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleAssignment = new RoleAssignment
        {
            Id = roleAssignmentId,
            OrganizationId = organizationId,
            UserId = userId,
            Role = Role.Administrator,
            IsSystemAssigned = true
        };

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _roleAssignmentRepository.GetAsync(organizationId, roleAssignmentId)
            .Returns(Task.FromResult(roleAssignment)!);

        var result = await _roleService.GetAssignmentAsync(organizationId, roleAssignmentId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Id, Is.EqualTo(roleAssignmentId));
        Assert.That(result.OrganizationId, Is.EqualTo(organizationId));
        Assert.That(result.UserId, Is.EqualTo(userId));
    }

    [Test]
    public async Task GetAssignmentAsync_WithNonExistentAssignment_ThrowsNotFoundException()
    {
        var organizationId = Guid.NewGuid();
        var roleAssignmentId = Guid.NewGuid();

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _roleAssignmentRepository.GetAsync(organizationId, roleAssignmentId)
            .ReturnsNull();

        var ex = Assert.ThrowsAsync<NotFoundException>(async () =>
            await _roleService.GetAssignmentAsync(organizationId, roleAssignmentId));

        Assert.That(ex!.Message, Is.EqualTo("Role assignment not found"));
    }

    [Test]
    public async Task GetAllAssignmentsAsync_ReturnsAllAssignments()
    {
        var organizationId = Guid.NewGuid();
        var assignments = new List<RoleAssignment>
        {
            new RoleAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = Guid.NewGuid(),
                Role = Role.Administrator,
                IsSystemAssigned = true
            },
            new RoleAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = Guid.NewGuid(),
                Role = Role.Viewer,
                IsSystemAssigned = false
            }
        };

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _roleAssignmentRepository.GetAllAsync(organizationId).Returns(Task.FromResult(assignments));

        var result = await _roleService.GetAllAssignmentsAsync(organizationId);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetUserAssignmentsAsync_ReturnsUserSpecificAssignments()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var assignments = new List<RoleAssignment>
        {
            new RoleAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                Role = Role.Administrator,
                IsSystemAssigned = true
            },
            new RoleAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId,
                Role = Role.ResourceManager,
                IsSystemAssigned = false
            }
        };

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _roleAssignmentRepository.GetUserAssignmentsAsync(organizationId, userId)
            .Returns(Task.FromResult(assignments));

        var result = await _roleService.GetUserAssignmentsAsync(organizationId, userId);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.All(r => r.UserId == userId), Is.True);
    }

    [Test]
    public async Task GetRoleAssignmentsSummaryAsync_GroupsAssignmentsByUser()
    {
        var organizationId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        
        var assignments = new List<RoleAssignment>
        {
            new RoleAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId1,
                Role = Role.Administrator,
                IsSystemAssigned = true
            },
            new RoleAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId1,
                Role = Role.ResourceManager,
                IsSystemAssigned = false
            },
            new RoleAssignment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = userId2,
                Role = Role.Viewer,
                IsSystemAssigned = false
            }
        };

        var users = new List<UserResponse>
        {
            new UserResponse(userId1.ToString(), "user1@example.com", "User", "1", null, true),
            new UserResponse(userId2.ToString(), "user2@example.com", "User", "2", null, true)
        };

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _roleAssignmentRepository.GetAllAsync(organizationId).Returns(Task.FromResult(assignments));
        _authClient.GetUsersAsync(organizationId).Returns(users);

        var result = await _roleService.GetRoleAssignmentsSummaryAsync(organizationId);

        Assert.That(result, Has.Count.EqualTo(2));
        
        var user1Summary = result.FirstOrDefault(s => s.UserEmail == "user1@example.com");
        Assert.That(user1Summary, Is.Not.Null);
        Assert.That(user1Summary!.Assignments, Has.Length.EqualTo(2));
        
        var user2Summary = result.FirstOrDefault(s => s.UserEmail == "user2@example.com");
        Assert.That(user2Summary, Is.Not.Null);
        Assert.That(user2Summary!.Assignments, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task GetRoleAssignmentsSummaryAsync_WithNoAssignments_ReturnsEmptyList()
    {
        var organizationId = Guid.NewGuid();
        var assignments = new List<RoleAssignment>();

        _organizationRepository.GetByIdAsync(organizationId)
            .Returns(new Organization { Id = organizationId, Deleted = false });
        _roleAssignmentRepository.GetAllAsync(organizationId).Returns(Task.FromResult(assignments));

        var result = await _roleService.GetRoleAssignmentsSummaryAsync(organizationId);

        Assert.That(result, Is.Empty);
    }
}
