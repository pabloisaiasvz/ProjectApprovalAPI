namespace ProjectApprovalAPI.DTOs
{
    public class ProjectProposalUpdateDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Duration { get; set; }
    }

}
