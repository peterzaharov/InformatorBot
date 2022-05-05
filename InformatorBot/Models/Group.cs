using InformatorBot.Models;

namespace InformatorBot
{
    public class Group
    {
        public int GroupId { get; set; }
        public string? Title { get; set; }
        public ICollection<User>? Users { get; set; }
    }
}
