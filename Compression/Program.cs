namespace Compression;

internal class Program
{
    private const string TxtExtension = "txt";
    private const string HufExtension = "huf";

    private static void Main(string[] args)
    {
        try
        {
            var inputFile = Validate(args);
            var fileExtension = Path.GetExtension(inputFile).TrimStart('.');

            switch (fileExtension)
            {
                case TxtExtension:
                    Huffman.Compress(inputFile, $"{inputFile}.huf");
                    break;
                case HufExtension:
                    var fileName = Path.GetFileNameWithoutExtension(inputFile);
                    Huffman.Decompress(inputFile, $"{fileName}.decompressed.{TxtExtension}");
                    break;
                default:
                    throw new ArgumentException($"Unsupported file extension: {fileExtension}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static string Validate(string[] args)
    {
        if (args.Length == 0)
        {
            throw new ArgumentException("Please provide input file.");
        }

        var inputFile = args[0];
        if (!File.Exists(inputFile))
        {
            throw new FileNotFoundException("Could not find input file, please check if path is correct.");
        }

        return inputFile;
    }
}