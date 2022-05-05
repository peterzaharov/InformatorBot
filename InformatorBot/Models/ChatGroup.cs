namespace InformatorBot
{
    public class ChatGroup
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public virtual Group? Group { get; set; }
        public string? ChatId { get; set; }
        public virtual Chat? Chat { get; set; }
        public bool IsDeleted { get; set; }
    }
}
