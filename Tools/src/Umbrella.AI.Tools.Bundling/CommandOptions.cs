namespace Umbrella.AI.Tools.Bundling;

public sealed class CommandOptions
{
    public string TargetPath { get; set; } = Directory.GetCurrentDirectory();
    public bool Force { get; set; }
    public bool AllowNonRepo { get; set; }
    public bool CleanEmptyMcp { get; set; }
}