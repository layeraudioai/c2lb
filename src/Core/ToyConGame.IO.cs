using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ToyConEngine
{
    public partial class ToyConGame
    {
        
        private string? PromptForSavePath(string defaultName, string filter)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    string safeDefault = EscapePowerShellString(defaultName);
                    string safeFilter = EscapePowerShellString(filter);
                    string cmd = $"Add-Type -AssemblyName System.Windows.Forms; $d = New-Object System.Windows.Forms.SaveFileDialog; $d.Filter = '{safeFilter}'; $d.FileName = '{safeDefault}'; $d.InitialDirectory = '{EscapePowerShellString(AppDomain.CurrentDomain.BaseDirectory)}'; if ($d.ShowDialog() -eq 'OK') {{ $d.FileName }}";
                    var psi = new ProcessStartInfo("powershell", $"-NoProfile -NonInteractive -STA -command \"{cmd}\"")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p is null) return null;
                    string res = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    return string.IsNullOrEmpty(res) ? null : res;
                }
                catch { }
                return null;
            }
            return defaultName;
        }

        private string? PromptForOpenPath(string filter)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    string safeFilter = EscapePowerShellString(filter);
                    string cmd = $"Add-Type -AssemblyName System.Windows.Forms; $d = New-Object System.Windows.Forms.OpenFileDialog; $d.Filter = '{safeFilter}'; $d.InitialDirectory = '{EscapePowerShellString(AppDomain.CurrentDomain.BaseDirectory)}'; if ($d.ShowDialog() -eq 'OK') {{ $d.FileName }}";
                    var psi = new ProcessStartInfo("powershell", $"-NoProfile -NonInteractive -STA -command \"{cmd}\"")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p is null) return null;
                    string res = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    return string.IsNullOrEmpty(res) ? null : res;
                }
                catch { }
            }
            return null;
        }

        private void SaveLayout(string filename)
        {
            string path = Path.IsPathRooted(filename) ? filename : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, SerializeGraph());
        }

        private void LoadLayoutFromLines(string[] lines)
        {
            _engine.Nodes.Clear();
            _nodeRects.Clear();
            _selectedNodes.Clear();
            _inspectedNode = null;
            _connectionStartNode = null;

            if (lines.Length == 0 || lines[0] != "TOYCON_v1") return;

            var idToNode = new Dictionary<int, Node>();

            foreach (var line in lines)
            {
                var parts = line.Split(' ');
                if (parts[0] == "NODE")
                {
                    int id = int.Parse(parts[1]);
                    string type = parts[2];
                    int x = int.Parse(parts[3]);
                    int y = int.Parse(parts[4]);
                    string data = parts.Length > 5 ? string.Join(" ", parts.Skip(5)) : "";

                    Node? n = null;
                    if (type == "ConstantNode") n = new ConstantNode(0);
                    else if (type == "MathNode") n = new MathNode(MathNode.Operation.Add);
                    else if (type == "LogicNode") n = new LogicNode(LogicNode.LogicType.And);
                    else if (type == "TimerNode") n = new TimerNode();
                    else if (type == "CounterNode") n = new CounterNode();
                    else if (type == "RandomNode") n = new RandomNode();
                    else if (type == "ButtonNode") n = new ButtonNode();
                    else if (type == "KeyNode") n = new KeyNode();
                    else if (type == "CursorNode") n = new CursorNode();
                    else if (type == "ColorOutputNode") n = new ColorOutputNode();
                    else if (type == "BeepOutputNode") n = new BeepOutputNode();
                    else if (type == "ScreenNode") n = new ScreenNode();
                    else if (type == "ScriptImporterNode") n = new ScriptImporterNode();

                    if (n != null)
                    {
                        ApplyNodeData(n, data);
                        SpawnNodeAt(n, x, y);
                        idToNode[id] = n;
                    }
                }
                else if (parts[0] == "CONN")
                {
                    int srcId = int.Parse(parts[1]);
                    int srcSlot = int.Parse(parts[2]);
                    int tgtId = int.Parse(parts[3]);
                    int tgtSlot = int.Parse(parts[4]);

                    if (idToNode.ContainsKey(srcId) && idToNode.ContainsKey(tgtId))
                    {
                        _engine.Connect(idToNode[srcId], srcSlot, idToNode[tgtId], tgtSlot);
                    }
                }
            }
        }

        private void LoadLayout(string filename)
        {
            string path = Path.IsPathRooted(filename) ? filename : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            if (!File.Exists(path)) return;
            LoadLayoutFromLines(File.ReadAllLines(path));
        }

        private void ExportStandalone(string filename)
        {
            string exportPath = Path.IsPathRooted(filename) ? filename : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            if (!Path.HasExtension(exportPath)) exportPath += ".exe";
            string logDir = Path.GetDirectoryName(exportPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            string logPath = Path.Combine(logDir, "export_log.txt");
            void Log(string msg) => File.AppendAllText(logPath, $"{DateTime.Now}: {msg}\n");

            try
            {
                var currentExe = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(currentExe)) throw new InvalidOperationException("Unable to determine executable path.");
                var sourceDir = AppDomain.CurrentDomain.BaseDirectory;
                var destDir = Path.GetDirectoryName(exportPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                var tempDir = Path.Combine(destDir, "ToyCon_Temp_Build\\");
                var stagedExportPath = Path.Combine(tempDir, "ToyCon_Export.exe");

                // 1. Copy the current executable
                if (!Directory.Exists(tempDir)) 
                {
                    Directory.CreateDirectory(tempDir);
                }

                CopyDirectory(sourceDir, tempDir);
                File.Copy(currentExe, stagedExportPath, true);

                // 2. Prepare data
                string graphData = SerializeGraph();
                byte[] dataBytes = Encoding.UTF8.GetBytes(graphData);
                byte[] lengthBytes = BitConverter.GetBytes(dataBytes.Length);
                byte[] magicBytes = Encoding.UTF8.GetBytes(StandaloneMagic); // 10 bytes

                // 3. Append data to the end of the new executable
                using (var stream = new FileStream(stagedExportPath, FileMode.Append))
                {
                    stream.Write(dataBytes, 0, dataBytes.Length);
                    stream.Write(lengthBytes, 0, lengthBytes.Length);
                    stream.Write(magicBytes, 0, magicBytes.Length);
                }

                // 4. Find Packer
                string baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
                string packerPath = Path.Combine(baseDir, "tools", "packer.exe");
                string loaderPath = Path.Combine(baseDir, "tools", "loader.exe");

                if (!File.Exists(packerPath))
                {
                    var dir = new DirectoryInfo(baseDir);
                    while (dir != null)
                    {
                        var check = Path.Combine(dir.FullName, "tools", "packer.exe");
                        if (File.Exists(check))
                        {
                            packerPath = check;
                            loaderPath = Path.Combine(dir.FullName, "tools", "loader.exe");
                            break;
                        }
                        dir = dir.Parent;
                    }
                }

                // 5. Run Packer
                if (File.Exists(packerPath))
                {
                    // Ensure loader.exe is present in temp dir for packer
                    if (File.Exists(loaderPath))
                    {
                        File.Copy(loaderPath, Path.Combine(tempDir, "loader.exe"), true);
                    }

                    var packerDirectory = Path.GetDirectoryName(packerPath) ?? baseDir;
                    var psi = new ProcessStartInfo(packerPath)
                    {
                        ArgumentList = { tempDir, stagedExportPath, exportPath },
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = packerDirectory
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit();
                }

                // 6. Cleanup
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch (Exception ex) { Log($"Cleanup error: {ex.Message}"); }
            }
            catch (Exception e)
            {
                Log($"CRITICAL ERROR: {e.Message}\n{e.StackTrace}");
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            var dir = Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                try
                {
                    string destFile = Path.Combine(destDir, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }
                catch { }
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                try
                {
                    if (new DirectoryInfo(subDir).FullName == dir.FullName) continue;
                    string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                    CopyDirectory(subDir, destSubDir);
                }
                catch { }
            }
        }

        private bool TryLoadEmbeddedLayout()
        {
            try
            {
                var currentExe = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(currentExe)) return false;
                using (var stream = new FileStream(currentExe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < 20) return false; // Magic(10) + Int(4) + minimal data

                    byte[] magicCheck = new byte[10];
                    stream.Seek(-10, SeekOrigin.End);
                    stream.Read(magicCheck, 0, 10);
                    string magic = Encoding.UTF8.GetString(magicCheck);
                    
                    if (magic != StandaloneMagic) return false;

                    byte[] lengthCheck = new byte[4];
                    stream.Seek(-14, SeekOrigin.End);
                    stream.Read(lengthCheck, 0, 4);
                    int dataLength = BitConverter.ToInt32(lengthCheck, 0);

                    byte[] data = new byte[dataLength];
                    stream.Seek(-(14 + dataLength), SeekOrigin.End);
                    stream.Read(data, 0, dataLength);

                    string layout = Encoding.UTF8.GetString(data);
                    // Split by newline, handling both \r\n and \n
                    LoadLayoutFromLines(layout.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None));
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
