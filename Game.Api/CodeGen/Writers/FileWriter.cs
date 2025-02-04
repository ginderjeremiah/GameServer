namespace Game.Api.CodeGen.Writers
{
    internal class FileWriter
    {
        public static void OverwriteFileIfTextDiffers(string filePath, string text)
        {
            if (!File.Exists(filePath) || File.ReadAllText(filePath) != text)
            {
                File.WriteAllText(filePath, text);
            }
        }
    }
}
