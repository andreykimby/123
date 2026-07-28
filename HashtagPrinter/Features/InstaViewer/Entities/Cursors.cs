namespace Skarynagram.Features.InstaViewer.Entities
{
    public class Cursors
    {
        public required string After { get; set; } = string.Empty;

        public static Cursors Empty => new Cursors { After = string.Empty };
    }
}