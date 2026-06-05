namespace Game.Api.CodeGen
{
    public class CodeGenOptions
    {
        public required string TargetDirectory { get; set; }
        public string NewLine { get; set; } = Environment.NewLine;
    }
}
