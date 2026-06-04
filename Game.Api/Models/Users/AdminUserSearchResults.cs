namespace Game.Api.Models.Users
{
    /// <summary>
    /// A page of users matching an admin search, along with the total number of matches across all pages.
    /// </summary>
    public class AdminUserSearchResults : IModel
    {
        public required IEnumerable<AdminUser> Users { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
