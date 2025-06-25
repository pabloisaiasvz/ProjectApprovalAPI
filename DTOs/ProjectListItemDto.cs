namespace ProjectApprovalAPI.DTOs
{
    namespace ProjectApprovalAPI.DTOs
    {
        public class ProjectListItemDto
        {
            public Guid Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public decimal Amount { get; set; }
            public int Duration { get; set; }
            public string Area { get; set; }
            public string Status { get; set; }
            public string Type { get; set; }
            public DateTime CreatedAt { get; set; }
            public string ApplicantUserName { get; set; }
            public int ApplicantUserId { get; set; }
            public int StatusId { get; set; }
            public string StatusName { get; set; }
        }

    }

}
