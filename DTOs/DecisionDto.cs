using ProjectApprovalAPI.Enums;

namespace ProjectApprovalAPI.DTOs
{
    public class DecisionDto
    {
        public int Id { get; set; }
        public int User { get; set; }
        public int Status { get; set; } 
        public string? Observation { get; set; }
    }
}

