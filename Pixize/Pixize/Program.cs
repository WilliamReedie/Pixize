using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

class Program
{
    static void Main(string[] args)
    {
        string? path = null;
        const int defaultScale = 2;
        int scale = defaultScale;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg == "-s" || arg == "--scale")
            {
                if (i + 1 >= args.Length)
                {
                    PrintError("Option requires an argument: " + arg);
                    PrintUsage();
                    Environment.Exit(1);
                }

                string next = args[i + 1];

                if (!int.TryParse(next, out int parsed) || parsed <= 0)
                {
                    PrintError($"Invalid scale value '{next}'. Must be a positive integer.");
                    PrintUsage();
                    Environment.Exit(1);
                }

                scale = parsed;
                i++; // consume value
            }
            else if (arg == "-p" || arg == "--path")
            {
                if (i + 1 >= args.Length)
                {
                    PrintError("Option requires an argument: " + arg);
                    PrintUsage();
                    Environment.Exit(1);
                }

                string next = args[i + 1];

                if (string.IsNullOrWhiteSpace(next))
                {
                    PrintError("Provided path is empty.");
                    PrintUsage();
                    Environment.Exit(1);
                }

                if (path != null)
                {
                    PrintError("Multiple input paths provided.");
                    PrintUsage();
                    Environment.Exit(1);
                }

                path = next;
                i++; // consume value
            }
            else if (arg.StartsWith("-"))
            {
                PrintError($"Unknown option '{arg}'.");
                PrintUsage();
                Environment.Exit(1);
            }
            else
            {
                // First non-option argument is treated as the input file path.
                if (path == null)
                {
                    if (string.IsNullOrWhiteSpace(arg))
                    {
                        PrintError("Provided path is empty.");
                        PrintUsage();
                        Environment.Exit(1);
                    }

                    path = arg;
                }
                else
                {
                    PrintError($"Unexpected argument '{arg}'. Only one input file path is expected.");
                    PrintUsage();
                    Environment.Exit(1);
                }
            }
        }

        // Validate presence
        if (string.IsNullOrWhiteSpace(path))
        {
            PrintError("No input file provided.");
            PrintUsage();
            Environment.Exit(1);
        }

        // Validate path format and existence
        try
        {
            string fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                PrintError($"File not found: {fullPath}");
                Environment.Exit(1);
            }

            path = fullPath;
        }
        catch (Exception ex)
        {
            PrintError($"Invalid file path '{path}': {ex.Message}");
            Environment.Exit(1);
        }

        ScalePix(path, scale);
    }

    static void ScalePix(string filePath, int scale)
    {
        if (scale <= 0)
        {
            PrintError("Scale must be a positive integer.");
            Environment.Exit(1);
        }

        // Verify extension is .png
        string ext = Path.GetExtension(filePath) ?? string.Empty;
        if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            PrintError("Only PNG files are supported at this time.");
            Environment.Exit(1);
        }

        // Optional: quick signature check for PNG (first 8 bytes)
        byte[] pngSignature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        try
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] header = new byte[8];
                int read = fs.Read(header, 0, header.Length);
                if (read != header.Length || !AreEqual(header, pngSignature))
                {
                    PrintError("File does not look like a valid PNG.");
                    Environment.Exit(1);
                }
            }
        }
        catch (Exception ex)
        {
            PrintError($"Error reading file header: {ex.Message}");
            Environment.Exit(1);
        }

        // Load and scale image
        try
        {
            using (var original = (Bitmap)Image.FromFile(filePath))
            {
                int newWidth = original.Width * scale;
                int newHeight = original.Height * scale;

                using (var scaled = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb))
                {
                    scaled.SetResolution(original.HorizontalResolution, original.VerticalResolution);

                    using (var g = Graphics.FromImage(scaled))
                    {
                        // For pixel-art style scaling, use NearestNeighbor. For photographic, consider HighQualityBicubic.
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.CompositingMode = CompositingMode.SourceOver;
                        g.CompositingQuality = CompositingQuality.HighSpeed;
                        g.FillRectangle(Brushes.Transparent, 0, 0, newWidth, newHeight);
                        g.DrawImage(original, 0, 0, newWidth, newHeight);
                    }

                    // Build output path with "x{scale}" appended before the extension.
                    string dir = Path.GetDirectoryName(filePath) ?? "";
                    string name = Path.GetFileNameWithoutExtension(filePath);
                    string outFileName = $"{name}x{scale}.png";
                    string outPath = Path.Combine(dir, outFileName);

                    // If file exists, avoid overwriting by appending an index.
                    int index = 1;
                    while (File.Exists(outPath))
                    {
                        outFileName = $"{name}x{scale}_{index}.png";
                        outPath = Path.Combine(dir, outFileName);
                        index++;
                    }

                    // Save as PNG
                    scaled.Save(outPath, ImageFormat.Png);

                    Console.WriteLine($"Wrote scaled image to: {outPath}");
                }
            }
        }
        catch (Exception ex)
        {
            PrintError($"Failed to scale image: {ex.Message}");
            Environment.Exit(1);
        }

        static bool AreEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }

    static void PrintError(string message)
    {
        Console.Error.WriteLine(message);
    }

    static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: Pixize [options] <file>");
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  -s, --scale <n>    Scale factor (positive integer). Default: 2");
        Console.Error.WriteLine("  -p, --path <file>  Input file path (alternative to positional argument)");
    }
}