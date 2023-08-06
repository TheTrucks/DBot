namespace DBot.Models.Options
{
    internal sealed class AppOptions
    {
        public required string BaseURL { get; set; }
        public required string WSSAddress { get; set; }
        public required string AppID { get; set; }
        public required string HttpCatsBaseURL { get; set; }
    }
}
