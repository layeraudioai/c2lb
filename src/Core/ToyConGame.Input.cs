using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ToyConEngine
{
    public partial class ToyConGame
    {
        private bool IsKeyPressed(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && !_prevKeyboardState.IsKeyDown(key);
        }

        private void HandleTextInput(KeyboardState current, ref string buffer)
        {
            foreach (Keys key in current.GetPressedKeys())
            {
                if (!_prevKeyboardState.IsKeyDown(key))
                {
                    if (key == Keys.Back && buffer.Length > 0)
                        buffer = buffer.Substring(0, buffer.Length - 1);
                    else
                    {
                        char? c = KeyToChar(key);
                        if (c.HasValue) buffer += c.Value;
                    }
                }
            }
        }

        private string GetClipboard()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo("powershell", "-command \"Get-Clipboard\"");
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    psi.CreateNoWindow = true;
                    using var p = Process.Start(psi);
                    if (p is null) return "";
                    string text = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    return text.TrimEnd();
                }
            }
            catch { }
            return "";
        }

        private char? KeyToChar(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9) return (char)('0' + (key - Keys.D0));
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return (char)('0' + (key - Keys.NumPad0));
            if (key == Keys.OemPeriod || key == Keys.Decimal) return '.';
            if (key == Keys.OemMinus || key == Keys.Subtract) return '-';
            return null;
        }

        private char? ScriptKeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z) return shift ? key.ToString()[0] : key.ToString().ToLower()[0];
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                string s = (key - Keys.D0).ToString();
                if (shift)
                {
                    if (s == "9") return '(';
                    if (s == "0") return ')';
                    if (s == "8") return '*';
                    if (s == "5") return '%';
                }
                return s[0];
            }
            if (key == Keys.OemPlus || key == Keys.Add) return shift ? '+' : '=';
            if (key == Keys.OemMinus || key == Keys.Subtract) return shift ? '_' : '-';
            if (key == Keys.OemPeriod) return shift ? '>' : '.';
            if (key == Keys.OemComma) return shift ? '<' : ',';
            if (key == Keys.OemSemicolon) return ';';
            if (key == Keys.OemQuestion) return shift ? '?' : '/';
            return null;
        }
    }
}
