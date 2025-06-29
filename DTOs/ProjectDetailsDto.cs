using ProjectApprovalAPI.DTOs.ProjectApprovalAPI.DTOs;

namespace ProjectApprovalAPI.DTOs
{
    public class ProjectDetailsDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public int Duration { get; set; }
        public AreaDto Area { get; set; }
        public ApprovalStatusDto Status { get; set; }
        public ProjectTypeDto Type { get; set; }
        public UserDto User { get; set; }
        public List<ApprovalStepDetailsDto> Steps { get; set; }
    }
}
