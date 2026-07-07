using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
// No warnings: code refactored to avoid nullable warnings
namespace ToyConEngine.Tools
{
    internal static class SampleGenerator
    {
        public static void Run(string[] args)
        {
            // Determine sample list path: use first arg if provided and exists, otherwise search upward
            string listPath;
            if (args != null && args.Length > 0 && File.Exists(args[0]))
            {
                listPath = Path.GetFullPath(args[0]);
            }
            else
            {
                // Robust fallback search: walk up from base, then CWD, then exe directory
                string? foundPath = null;
                // 1. Walk up directories from base
                var dirInfo = new DirectoryInfo(AppContext.BaseDirectory);
                while (dirInfo != null)
                {
                    var candidate = Path.Combine(dirInfo.FullName, "MidiInstrumentSamplesList.md");
                    if (File.Exists(candidate))
                    {
                        foundPath = candidate;
                        break;
                    }
                    dirInfo = dirInfo.Parent;
                }
                // 2. Current working directory ("%CD%")
                if (foundPath == null)
                {
                    var cwdCandidate = Path.Combine(Environment.CurrentDirectory, "MidiInstrumentSamplesList.md");
                    if (File.Exists(cwdCandidate)) foundPath = cwdCandidate;
                }
                // 3. Executable directory ("%~dp0" style)
                if (foundPath == null)
                {
                    var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    if (!string.IsNullOrEmpty(exeDir))
                    {
                        var exeCandidate = Path.Combine(exeDir, "MidiInstrumentSamplesList.md");
                        if (File.Exists(exeCandidate)) foundPath = exeCandidate;
                    }
                }
                if (foundPath == null)
                    throw new FileNotFoundException("Sample list not found.", "MidiInstrumentSamplesList.md");
                listPath = foundPath;
            }
            if (!File.Exists(listPath))
                throw new FileNotFoundException("Sample list not found.", listPath);
            // Set rootDir based on location of listPath
            string? maybeRoot = Path.GetDirectoryName(listPath) ?? throw new InvalidOperationException("Unable to determine directory of sample list.");
            string rootDir = maybeRoot;
            // Determine output directory: second argument or prompt the user
            string outputDir;
            if (args != null && args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                outputDir = args[1];
            }
            else
            {
                Console.Write("Enter output directory (project root directory full path): ");
                var input = Console.ReadLine();
                outputDir = string.IsNullOrWhiteSpace(input) ? Directory.GetCurrentDirectory() : input;
            }
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);
            string contentDir = Path.Combine(outputDir, "Content");

            // List path already validated above

            // 1️⃣ Parse the markdown table to obtain instrument names (128 entries)
            var instrumentNames = ParseInstrumentNames(listPath);

            // 2️⃣ Ensure each instrument folder exists and create 32 placeholder WAVs
            int icount=0;
	    foreach (var instrument in instrumentNames)
            {
                string instrumentDir = Path.Combine(contentDir, instrument);
                Directory.CreateDirectory(instrumentDir);

                for (int i = 1; i <= 32; i++)
                {
                    string fileName = $"{instrument}-{i:D2}.wav"; // 01‑32
                    string targetPath = Path.Combine(instrumentDir, fileName);
                    if (!File.Exists(targetPath))
                    {
                        WritePlaceholderWav(targetPath, durationSec: 0.1, 44100, 66 + (icount /128) + (i * 66));
                        Console.WriteLine($"[CREATE] {targetPath}");
                    }
                }
                icount++;
            }

            // 3️⃣ Generate 32 drum‑kit folders, each with 32 placeholder samples
            GenerateDrumKits(contentDir);
        }

        // --------------------------------------------------------------------
        // Extract instrument names from the markdown table (column 2).
        // --------------------------------------------------------------------
        private static List<string> ParseInstrumentNames(string mdPath)
        {
            var names = new List<string>();
            var lines = File.ReadAllLines(mdPath);
            // Table rows look like: | 0 | ACOUSTIC_GRAND_PIANO | `Content/ACOUSTIC_GRAND_PIANO/...` |
            var rowPattern = new Regex(@"\|\s*\d+\s*\|\s*(\S+)\s*\|", RegexOptions.Compiled);
            foreach (var line in lines)
            {
                var m = rowPattern.Match(line);
                if (!m.Success) continue;
                string instrument = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(instrument))
                    names.Add(instrument);
            }
            return names;
        }

        // --------------------------------------------------------------------
        // Write a small 0.1 s 440 Hz sine‑wave WAV (16‑bit PCM mono).
        // --------------------------------------------------------------------
        private static void WritePlaceholderWav(string filePath, double durationSec, int sampleRate = 44100, double frequency = 440.0)
        {
            int totalSamples = (int)(durationSec * sampleRate);
            short[] data = new short[totalSamples];

            double increment = 2 * Math.PI * frequency / sampleRate;
            double phase = 0;
            for (int i = 0; i < totalSamples; i++)
            {
                // 20 % volume to keep the placeholder unobtrusive.
                data[i] = (short)(Math.Sin(phase) * short.MaxValue * 0.2);
                phase += increment;
            }

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            // ---- RIFF header -------------------------------------------------
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + data.Length * 2);               // file size - 8 bytes
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            // ---- fmt sub‑chunk -----------------------------------------------
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);                                 // SubChunk1Size (PCM)
            bw.Write((short)1);                           // AudioFormat = PCM
            bw.Write((short)1);                           // NumChannels = mono
            bw.Write(sampleRate);                         // SampleRate
            bw.Write(sampleRate * 2);                     // ByteRate = SampleRate * BlockAlign
            bw.Write((short)2);                           // BlockAlign = NumChannels * BitsPerSample/8
            bw.Write((short)16);                          // BitsPerSample

            // ---- data sub‑chunk -----------------------------------------------
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(data.Length * 2);
            foreach (short s in data) bw.Write(s);
        }

        // --------------------------------------------------------------------
        // Create 32 drum‑kit folders (Kit01‑Kit32) each containing 32 placeholders.
        // --------------------------------------------------------------------
        private static void GenerateDrumKits(string contentDir)
        {
            string drumkitsRoot = Path.Combine(contentDir, "Drumkits");
            Directory.CreateDirectory(drumkitsRoot);

            for (int kitIndex = 1; kitIndex <= 32; kitIndex++)
            {
                string kitName = $"Kit{kitIndex:D2}"; // Kit01 … Kit32
                string kitDir = Path.Combine(drumkitsRoot, kitName);
                Directory.CreateDirectory(kitDir);

                for (int sampleIdx = 1; sampleIdx <= 32; sampleIdx++)
                {
                    string fileName = $"{kitName}-{sampleIdx:D2}.wav";
                    string targetPath = Path.Combine(kitDir, fileName);
                    if (!File.Exists(targetPath))
                    {
                        WritePlaceholderWav(targetPath, durationSec: 0.1, 44100, 20 + (kitIndex / 66) + (sampleIdx * 66));
                        Console.WriteLine($"[CREATE] {targetPath}");
                    }
                }
            }
        }
    }
}
