using ProjectApprovalAPI.Data;
using ProjectApprovalAPI.DTOs;
using ProjectApprovalAPI.Models;
using ProjectApprovalAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using ProjectApprovalAPI.DTOs.ProjectApprovalAPI.DTOs;
using ProjectApprovalAPI.Exceptions;

public class ProjectService : IProjectService
{
    private readonly ProjectApprovalDbContext _context;

    public ProjectService(ProjectApprovalDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> CreateProposalAsync(ProjectProposalDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new BusinessException("El título es obligatorio.");
        if (string.IsNullOrWhiteSpace(dto.Description))
            throw new BusinessException("La descripción es obligatoria.");
        if (dto.Amount <= 0)
            throw new BusinessException("El monto debe ser mayor a cero.");
        if (dto.Duration <= 0)
            throw new BusinessException("La duración debe ser mayor a cero.");
        if (!await _context.Areas.AnyAsync(a => a.Id == dto.Area))
            throw new BusinessException("Área inválida.");
        if (!await _context.ProjectTypes.AnyAsync(t => t.Id == dto.Type))
            throw new BusinessException("Tipo de proyecto inválido.");
        if (!await _context.Users.AnyAsync(u => u.Id == dto.User))
            throw new BusinessException("Usuario inválido.");
        if (await _context.ProjectProposals.AnyAsync(p => p.Title == dto.Title))
            throw new BusinessException("Ya existe un proyecto con ese título.");

        var initialStatus = await _context.ApprovalStatuses.FirstAsync();

        var proposal = new ProjectProposal
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            AreaId = dto.Area,
            TypeId = dto.Type,
            EstimatedAmount = dto.Amount,
            EstimatedDuration = dto.Duration,
            StatusId = initialStatus.Id,
            CreateAt = DateTime.UtcNow,
            CreateById = dto.User
        };

        _context.ProjectProposals.Add(proposal);

        var steps = await GenerateApprovalStepsAsync(proposal);
        _context.ProjectApprovalSteps.AddRange(steps);

        await _context.SaveChangesAsync();

        return proposal.Id;
    }

    private async Task<List<ProjectApprovalStep>> GenerateApprovalStepsAsync(ProjectProposal proposal)
    {
        var users = await _context.Users.ToListAsync();

        var rules = await _context.ApprovalRules
            .Where(r =>
                (r.MinAmount == 0 || proposal.EstimatedAmount >= r.MinAmount) &&
                (r.MaxAmount == 0 || proposal.EstimatedAmount <= r.MaxAmount) &&
                (r.AreaId == null || r.AreaId == proposal.AreaId) &&
                (r.TypeId == null || r.TypeId == proposal.TypeId)
            )
            .OrderBy(r => r.StepOrder)
            .ToListAsync();

        var steps = new List<ProjectApprovalStep>();
        var assignedUserIds = new HashSet<int>();

        foreach (var rule in rules)
        {
            var user = users.FirstOrDefault(u => u.RoleId == rule.ApproverRoleId && !assignedUserIds.Contains(u.Id));
            if (user == null)
                continue;

            assignedUserIds.Add(user.Id);

            steps.Add(new ProjectApprovalStep
            {
                ProjectProposalId = proposal.Id,
                StepOrder = rule.StepOrder,
                ApproverRoleId = rule.ApproverRoleId,
                ApproverUserId = user.Id,
                StatusId = 1
            });
        }

        return steps;
    }

    public async Task<List<ProjectProposal>> SearchAsync(string? title, int? statusId, int? creatorId, int? approverId)
    {
        var query = _context.ProjectProposals
            .Include(p => p.ApprovalSteps)
            .AsQueryable();

        if (!string.IsNullOrEmpty(title))
            query = query.Where(p => p.Title.Contains(title));

        if (statusId.HasValue)
            query = query.Where(p => p.StatusId == statusId);

        if (creatorId.HasValue)
            query = query.Where(p => p.CreateById == creatorId);

        if (approverId.HasValue)
            query = query.Where(p => p.ApprovalSteps.Any(a => a.ApproverUserId == approverId));

        return await query.ToListAsync();
    }

    public async Task<bool> UpdateProposalAsync(Guid id, ProjectProposalUpdateDto dto)
    {
        var proposal = await _context.ProjectProposals.FindAsync(id);

        if (proposal == null)
            throw new NotFoundException("La propuesta no fue encontrada.");

        if (proposal.StatusId != 4)
            throw new BusinessException("Solo se pueden editar propuestas con estado 'Observed'.");

        if (await _context.ProjectProposals.AnyAsync(p => p.Title == dto.Title && p.Id != id))
            throw new BusinessException("Ya existe un proyecto con ese título.");

        proposal.Title = dto.Title;
        proposal.Description = dto.Description;
        proposal.EstimatedDuration = dto.Duration;

        proposal.StatusId = 1;

        await _context.SaveChangesAsync();
        return true;
    }


    private async Task UpdateProposalStatusAsync(Guid projectId)
    {
        var steps = await _context.ProjectApprovalSteps
            .Where(s => s.ProjectProposalId == projectId)
            .ToListAsync();

        var proposal = await _context.ProjectProposals.FindAsync(projectId);
        if (proposal == null)
            throw new NotFoundException("Proyecto no encontrado.");

        if (steps.Any(s => s.StatusId == 3))
        {
            proposal.StatusId = 3;
        }
        else if (steps.Any(s => s.StatusId == 4))
        {
            proposal.StatusId = 4;
        }
        else if (steps.All(s => s.StatusId == 2))
        {
            proposal.StatusId = 2;
        }
        else
        {
            proposal.StatusId = 1;
        }

        await _context.SaveChangesAsync();
    }


    public async Task<bool> ApproveAsync(Guid projectId, int stepId, int approverUserId)
    {
        var step = await _context.ProjectApprovalSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.ProjectProposalId == projectId);

        if (step == null)
            return false;

        if (step.StatusId != 1 && step.StatusId != 4)
            return false;

        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == approverUserId);
        if (user == null || user.RoleId != step.ApproverRoleId)
            return false;

        var previousSteps = await _context.ProjectApprovalSteps
            .Where(s => s.ProjectProposalId == projectId && s.StepOrder < step.StepOrder)
            .ToListAsync();

        if (previousSteps.Any(s => s.StatusId == 1))
            return false;

        step.StatusId = 2;
        step.ApproverUserId = approverUserId;
        step.DecisionDate = DateTime.UtcNow;

        await UpdateProposalStatusAsync(projectId);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ObserveAsync(Guid projectId, int stepId, int approverUserId, string observation)
    {
        var step = await _context.ProjectApprovalSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.ProjectProposalId == projectId);

        if (step == null)
            return false;

        if (step.StatusId == 2 || step.StatusId == 3)
            return false;

        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == approverUserId);
        if (user == null || user.RoleId != step.ApproverRoleId)
            return false;

        step.StatusId = 4;
        step.ApproverUserId = approverUserId;
        step.Observations = observation;
        step.DecisionDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await UpdateProposalStatusAsync(projectId);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectAsync(Guid projectId, int stepId, int approverUserId)
    {
        var step = await _context.ProjectApprovalSteps
            .FirstOrDefaultAsync(s => s.Id == stepId && s.ProjectProposalId == projectId);

        if (step == null)
            return false;

        if (step.StatusId == 2 || step.StatusId == 3)
            return false;

        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == approverUserId);
        if (user == null || user.RoleId != step.ApproverRoleId)
            return false;

        step.StatusId = 3;
        step.ApproverUserId = approverUserId;
        step.DecisionDate = DateTime.UtcNow;

        await UpdateProposalStatusAsync(projectId);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ProjectOptionsDto> GetProjectOptionsAsync()
    {
        return await Task.FromResult(new ProjectOptionsDto
        {
            Areas = new List<AreaDto>(),
            ProjectTypes = new List<ProjectTypeDto>(),
            Roles = new List<RoleDto>(),
            ApprovalStatuses = new List<ApprovalStatusDto>(),
            Users = new List<UserDto>()
        });
    }


    public async Task<IEnumerable<ProjectListItemDto>> GetFilteredAsync(ProjectFilterDto filter)
    {
        var query = _context.ProjectProposals
            .Include(p => p.Status)
            .Include(p => p.CreateBy)
            .AsQueryable();

        if (!string.IsNullOrEmpty(filter.Title))
            query = query.Where(p => p.Title.Contains(filter.Title));

        if (filter.StatusId.HasValue)
            query = query.Where(p => p.StatusId == filter.StatusId.Value);

        if (filter.ApplicantUserId.HasValue)
            query = query.Where(p => p.CreateById == filter.ApplicantUserId.Value);

        if (filter.ApprovalUserId.HasValue)
            query = query.Where(p => p.ApprovalSteps.Any(a => a.ApproverUserId == filter.ApprovalUserId.Value));

        var projects = await query.ToListAsync();

        return projects.Select(p => new ProjectListItemDto
        {
            Id = p.Id,
            Title = p.Title
        });
    }

    public async Task<DecisionOptionsDto> GetDecisionOptionsAsync(Guid projectId)
    {
        var project = await _context.ProjectProposals
            .Include(p => p.Status)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            throw new KeyNotFoundException("Proyecto no encontrado");

        var availableStatuses = await _context.ApprovalStatuses
            .Where(s => s.Id == 2 || s.Id == 3 || s.Id == 4)
            .Select(s => new ApprovalStatusDto
            {
                Id = s.Id,
                Name = s.Name
            })
            .ToListAsync();

        return new DecisionOptionsDto
        {
            AvailableStatuses = availableStatuses,
            CurrentStatus = project.Status.Name
        };
    }


    public async Task<ProjectDetailsDto?> GetByIdAsync(Guid id)
    {
        var proposal = await _context.ProjectProposals
            .Include(p => p.Area)
            .Include(p => p.Type)
            .Include(p => p.Status)
            .Include(p => p.CreateBy)
                .ThenInclude(u => u.Role)
            .Include(p => p.ApprovalSteps)
                .ThenInclude(s => s.ApproverRole)
            .Include(p => p.ApprovalSteps)
                .ThenInclude(s => s.Status)
            .Include(p => p.ApprovalSteps)
                .ThenInclude(s => s.ApproverUser)
                    .ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proposal == null)
            return null;

        var dto = new ProjectDetailsDto
        {
            Id = proposal.Id,
            Title = proposal.Title,
            Description = string.IsNullOrEmpty(proposal.Description) ? "Sin descripción" : proposal.Description,
            Amount = proposal.EstimatedAmount,
            Duration = proposal.EstimatedDuration,
            Area = new AreaDto
            {
                Id = proposal.Area.Id,
                Name = proposal.Area.Name
            },
            Status = new ApprovalStatusDto
            {
                Id = proposal.Status.Id,
                Name = proposal.Status.Name
            },
            Type = new ProjectTypeDto
            {
                Id = proposal.Type.Id,
                Name = proposal.Type.Name
            },
            User = new UserDto
            {
                Id = proposal.CreateBy.Id,
                Name = proposal.CreateBy.Name,
                Email = proposal.CreateBy.Email,
                Role = new RoleDto
                {
                    Id = proposal.CreateBy.Role?.Id ?? 0,
                    Name = proposal.CreateBy.Role?.Name ?? "Sin Rol"
                }
            },
            Steps = proposal.ApprovalSteps
                .OrderBy(s => s.StepOrder)
                .Select(step => new ApprovalStepDetailsDto
                {
                    Id = step.Id,
                    StepOrder = step.StepOrder,
                    DecisionDate = step.DecisionDate,
                    Observations = step.Observations,
                    // ✅ ORDEN CORRECTO: ApproverUser PRIMERO
                    ApproverUser = step.ApproverUser == null ? null : new UserDto
                    {
                        Id = step.ApproverUser.Id,
                        Name = step.ApproverUser.Name,
                        Email = step.ApproverUser.Email,
                        Role = new RoleDto
                        {
                            Id = step.ApproverUser.Role?.Id ?? 0,
                            Name = step.ApproverUser.Role?.Name ?? "Sin Rol"
                        }
                    },
                    ApproverRole = new RoleDto
                    {
                        Id = step.ApproverRole.Id,
                        Name = step.ApproverRole.Name
                    },
                    Status = new ApprovalStatusDto
                    {
                        Id = step.Status.Id,
                        Name = step.Status.Name
                    }
                }).ToList()
        };

        return dto;
    }


    public async Task<IEnumerable<ProjectListItemDto>> GetAllAsync(string? title = null, string? status = null, string? applicant = null, string? approvalUser = null)
    {
        int? statusParsed = string.IsNullOrEmpty(status) ? null : int.Parse(status);
        int? applicantParsed = string.IsNullOrEmpty(applicant) ? null : int.Parse(applicant);
        int? approvalUserParsed = string.IsNullOrEmpty(approvalUser) ? null : int.Parse(approvalUser);

        var query = _context.ProjectProposals
            .Include(p => p.CreateBy)
            .Include(p => p.Status)
            .Include(p => p.Area)
            .Include(p => p.Type)
            .AsQueryable();

        if (!string.IsNullOrEmpty(title))
            query = query.Where(p => p.Title.Contains(title));

        if (statusParsed.HasValue)
            query = query.Where(p => p.StatusId == statusParsed.Value);

        if (applicantParsed.HasValue)
            query = query.Where(p => p.CreateById == applicantParsed.Value);

        if (approvalUserParsed.HasValue)
            query = query.Where(p =>
                p.ApprovalSteps.Any(s => s.ApproverUserId == approvalUserParsed.Value) &&
                p.CreateById != approvalUserParsed.Value);

        var projects = await query
            .Select(p => new ProjectListItemDto
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                Amount = p.EstimatedAmount,
                Duration = p.EstimatedDuration,
                Area = p.Area != null ? p.Area.Name : string.Empty,
                Status = p.Status != null ? p.Status.Name : string.Empty,
                Type = p.Type != null ? p.Type.Name : string.Empty
            })
            .ToListAsync();

        return projects;
    }



    public async Task<EditProjectOptionsDto> GetEditProjectOptionsAsync(Guid projectId)
    {
        var project = await _context.ProjectProposals
            .Include(p => p.Area)
            .Include(p => p.Type)
            .Include(p => p.Status)
            .Include(p => p.CreateBy)
            .Include(p => p.ApprovalSteps)
            .ThenInclude(s => s.ApproverRole)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            throw new KeyNotFoundException("Proyecto no encontrado");

        var dto = new EditProjectOptionsDto
        {
            Project = new ProjectDto
            {
                Id = project.Id,
                Title = project.Title,
                Description = project.Description,
                AreaId = project.AreaId,
                ProjectTypeId = project.TypeId,
                StatusId = project.StatusId,
                Steps = project.ApprovalSteps.Select(s => new ApprovalStepDto
                {
                    Id = s.Id,
                    ProjectProposalId = s.ProjectProposalId,
                    ApproverUserId = s.ApproverUserId ?? 0,
                    ApproverRole = new RoleDto
                    {
                        Id = s.ApproverRole.Id,
                        Name = s.ApproverRole.Name
                    },
                    Status = new ApprovalStatusDto { Id = s.Status.Id, Name = s.Status.Name }
                }).ToList()
            },
            Areas = await _context.Areas.Select(a => new AreaDto { Id = a.Id, Name = a.Name }).ToListAsync(),
            ProjectTypes = await _context.ProjectTypes.Select(t => new ProjectTypeDto { Id = t.Id, Name = t.Name }).ToListAsync(),
            Roles = await _context.ApproverRoles.Select(r => new RoleDto { Id = r.Id, Name = r.Name }).ToListAsync(),
            ApprovalStatuses = await _context.ApprovalStatuses.Select(s => new ApprovalStatusDto { Id = s.Id, Name = s.Name }).ToListAsync(),
            Users = await _context.Users.Select(u => new UserDto { Id = u.Id, Name = u.Name }).ToListAsync()
        };

        return dto;
    }

    public async Task<IEnumerable<AreaDto>> GetAreasAsync()
    {
        return await _context.Areas
            .Select(a => new AreaDto { Id = a.Id, Name = a.Name })
            .ToListAsync();
    }

    public async Task<IEnumerable<ProjectTypeDto>> GetProjectTypesAsync()
    {
        return await _context.ProjectTypes
            .Select(pt => new ProjectTypeDto { Id = pt.Id, Name = pt.Name })
            .ToListAsync();
    }

    public async Task<IEnumerable<RoleDto>> GetRolesAsync()
    {
        return await _context.ApproverRoles
            .Select(r => new RoleDto { Id = r.Id, Name = r.Name })
            .ToListAsync();
    }

    public async Task<IEnumerable<ApprovalStatusDto>> GetApprovalStatusesAsync()
    {
        return await _context.ApprovalStatuses
            .Select(s => new ApprovalStatusDto { Id = s.Id, Name = s.Name })
            .ToListAsync();
    }

    public async Task<IEnumerable<UserDto>> GetUsersAsync()
    {
        return await _context.Users
            .Include(u => u.Role)
            .Select(static u => new UserDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role == null ? null : new RoleDto
                {
                    Id = u.Role.Id,
                    Name = u.Role.Name
                }
            })
            .ToListAsync();
    }

}
