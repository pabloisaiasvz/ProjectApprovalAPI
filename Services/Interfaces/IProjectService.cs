using ProjectApprovalAPI.DTOs;
using ProjectApprovalAPI.DTOs.ProjectApprovalAPI.DTOs;
using ProjectApprovalAPI.Models;

namespace ProjectApprovalAPI.Services.Interfaces
{
    public interface IProjectService
    {
        Task<Guid> CreateProposalAsync(ProjectProposalDto dto);
        Task<ProjectDetailsDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<ProjectListItemDto>> GetAllAsync(string? title = null, string? statusId = null, string? createdById = null, string? approverUserId = null);
        Task<IEnumerable<ProjectListItemDto>> GetFilteredAsync(ProjectFilterDto filter);
        Task<bool> UpdateProposalAsync(Guid id, ProjectProposalUpdateDto dto);

        Task<bool> ApproveAsync(Guid projectId, int stepId, int approverUserId);
        Task<bool> ObserveAsync(Guid projectId, int stepId, int approverUserId, string observation);
        Task<bool> RejectAsync(Guid projectId, int stepId, int approverUserId);

        Task<ProjectOptionsDto> GetProjectOptionsAsync();
        Task<DecisionOptionsDto> GetDecisionOptionsAsync(Guid projectId);
        Task<EditProjectOptionsDto> GetEditProjectOptionsAsync(Guid projectId);
        Task<IEnumerable<AreaDto>> GetAreasAsync();
        Task<IEnumerable<ProjectTypeDto>> GetProjectTypesAsync();
        Task<IEnumerable<RoleDto>> GetRolesAsync();
        Task<IEnumerable<ApprovalStatusDto>> GetApprovalStatusesAsync();
        Task<IEnumerable<UserDto>> GetUsersAsync();

    }


}
