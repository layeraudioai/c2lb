using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ToyConEngine
{
    public partial class ToyConGame
    {
        private void HandleScriptInput(KeyboardState current, ref string buffer)
        {
            bool ctrl = current.IsKeyDown(Keys.LeftControl) || current.IsKeyDown(Keys.RightControl);

            foreach (Keys key in current.GetPressedKeys())
            {
                if (!_prevKeyboardState.IsKeyDown(key))
                {
                    if (key == Keys.Back && buffer.Length > 0)
                        buffer = buffer.Substring(0, buffer.Length - 1);
                    else if (key == Keys.Enter)
                        buffer += "\n";
                    else if (key == Keys.Space)
                        buffer += " ";
                    else if (ctrl && key == Keys.V)
                    {
                        buffer += GetClipboard();
                    }
                    else if (!ctrl)
                    {
                        char? c = ScriptKeyToChar(key, current.IsKeyDown(Keys.LeftShift) || current.IsKeyDown(Keys.RightShift));
                        if (c.HasValue) buffer += c.Value;
                    }
                }
            }
        }

        private void ParseAndGenerateGraph(string script)
        {
            _engine.Nodes.Clear();
            _nodeRects.Clear();
            _selectedNodes.Clear();
            _inspectedNode = null;
            _connectionStartNode = null;
            _isDraggingNodes = false;

            string pattern = @"([(){},;=+\-*/><&|^!]+|\s+|[A-Za-z_][A-Za-z0-9_]*|[0-9.]+)";
            var tokens = Regex.Split(script, pattern)
                              .Where(t => !string.IsNullOrWhiteSpace(t))
                              .ToList();

            var variables = new Dictionary<string, Node>();
            int currentY = 100;
            int currentX = 100;
            int tokenIndex = 0;

            void Spawn(Node n)
            {
                SpawnNodeAt(n, currentX, currentY);
                currentY += 80;
                if (currentY > 400) { currentY = 100; currentX += 200; }
            }

            try
            {
                ParseBlock(tokens, ref tokenIndex, variables, null, Spawn);
            }
            catch { }
        }

        private void ParseAndGenerateMidiGraph(string midiPath, MidiImporterNode? importerNode)
        {
            _engine.Nodes.Clear();
            _nodeRects.Clear();
            _selectedNodes.Clear();
            _inspectedNode = null;
            _connectionStartNode = null;
            _isDraggingNodes = false;

            if (string.IsNullOrWhiteSpace(midiPath) || !File.Exists(midiPath))
            {
                if (importerNode != null) importerNode.LastImportMessage = "MIDI file not found";
                return;
            }

            MidiImportResult? result = null;
            try
            {
                result = MidiImporter.ParseFile(midiPath);
            }
            catch (Exception ex)
            {
                if (importerNode != null) importerNode.LastImportMessage = $"Error parsing MIDI: {ex.Message}";
                return;
            }

            if (result == null || result.Tracks.Count == 0)
            {
                if (importerNode != null) importerNode.LastImportMessage = "No MIDI tracks could be parsed";
                return;
            }

            int maxNotes = 200;
            int notesImported = 0;
            foreach (var track in result.Tracks)
            {
                if (notesImported >= maxNotes)
                {
                    track.Notes.Clear();
                    continue;
                }
                int allowed = maxNotes - notesImported;
                if (track.Notes.Count > allowed)
                {
                    track.Notes.RemoveRange(allowed, track.Notes.Count - allowed);
                }
                notesImported += track.Notes.Count;
            }

            int currentX = 80;
            int currentY = 80;

            var clock = new TimerNode { Name = "MIDI Clock" };
            SpawnNodeAt(clock, currentX, currentY);
            currentY += 90;

            int trackIndex = 0;
            foreach (var track in result.Tracks)
            {
                if (track.Notes.Count == 0) continue;

                var counter = new CounterNode { Name = $"{track.Name} Count" };
                SpawnNodeAt(counter, currentX, currentY);
                currentY += 70;

                int noteIndex = 0;
                foreach (var note in track.Notes)
                {
                    var threshold = new ConstantNode((float)note.TimeSeconds) { Name = $"{track.Name} Onset {noteIndex + 1}" };
                    var gate = new LogicNode(LogicNode.LogicType.GreaterThan) { Name = $"{track.Name} Gate {noteIndex + 1}" };
                    var pitch = new ConstantNode(Math.Clamp((float)(note.NoteNumber - 60) / 12f, -2f, 2f)) { Name = $"{track.Name} Pitch {noteIndex + 1}" };
                    var volume = new ConstantNode(Math.Clamp(note.Velocity / 127f, 0.1f, 1f)) { Name = $"{track.Name} Vol {noteIndex + 1}" };
                    var beep = new BeepOutputNode { Name = $"{track.Name} Note {noteIndex + 1}", SoundName = GetMidiSampleName(track, note, trackIndex) };

                    SpawnNodeAt(threshold, currentX + 160, currentY + noteIndex * 50);
                    SpawnNodeAt(gate, currentX + 320, currentY + noteIndex * 50);
                    SpawnNodeAt(pitch, currentX + 480, currentY + noteIndex * 50);
                    SpawnNodeAt(volume, currentX + 640, currentY + noteIndex * 50);
                    SpawnNodeAt(beep, currentX + 800, currentY + noteIndex * 50);

                    _engine.Connect(clock, 0, gate, 0);
                    _engine.Connect(threshold, 0, gate, 1);
                    _engine.Connect(gate, 0, beep, 0);
                    _engine.Connect(pitch, 0, beep, 2);
                    _engine.Connect(volume, 0, beep, 1);
                    _engine.Connect(gate, 0, counter, 0);

                    noteIndex++;
                }

                currentY += Math.Max(70, noteIndex * 50 + 20);
                trackIndex++;
            }

            if (importerNode != null) importerNode.LastImportMessage = $"Imported {result.Tracks.Count} tracks and {notesImported} note events";
        }

        private string GetMidiSampleName(MidiTrack track, MidiNoteEvent note, int trackIndex)
        {
            var families = new[] { "KICK", "SNARE", "HI-HAT", "CYMBAL", "OPEN HI-HAT", "TAMBOURINE", "REVERSE CYMBAL" };
            string family = track.IsPercussion
                ? (note.NoteNumber < 40 ? "KICK" : note.NoteNumber < 60 ? "SNARE" : note.NoteNumber < 80 ? "HI-HAT" : "CYMBAL")
                : (track.Program < 8 ? "KICK" : track.Program < 32 ? "SNARE" : track.Program < 64 ? "HI-HAT" : "CYMBAL");

            int familyIndex = Array.IndexOf(families, family);
            if (familyIndex < 0) familyIndex = (trackIndex + note.Program) % families.Length;
            family = families[familyIndex];

            int sampleIndex = 1 + ((trackIndex + note.NoteNumber + note.Program) % 30);
            return $"{family}-{sampleIndex:D2}";
        }

        private void ParseBlock(List<string> tokens, ref int index, Dictionary<string, Node> variables, Node? conditionNode, Action<Node> spawner)
        {
            while (index < tokens.Count)
            {
                string t = tokens[index];

                if (t == "}")
                {
                    index++;
                    return;
                }
                else if (t == "var" || t == "int" || t == "float")
                {
                    index++;
                    if (index >= tokens.Count) return;
                    string name = tokens[index++];
                    if (index < tokens.Count && tokens[index] == "=")
                    {
                        index++;
                        Node valNode = ParseExpression(tokens, ref index, variables, spawner);
                        variables[name] = valNode;
                    }
                    if (index < tokens.Count && tokens[index] == ";") index++;
                }
                else if (t == "if")
                {
                    index++;
                    if (index < tokens.Count && tokens[index] == "(") index++;
                    Node cond = ParseExpression(tokens, ref index, variables, spawner);
                    if (index < tokens.Count && tokens[index] == ")") index++;

                    Node effectiveCond = cond;
                    if (conditionNode != null)
                    {
                        var andNode = new LogicNode(LogicNode.LogicType.And);
                        spawner(andNode);
                        _engine.Connect(conditionNode, 0, andNode, 0);
                        _engine.Connect(cond, 0, andNode, 1);
                        effectiveCond = andNode;
                    }

                    if (index < tokens.Count && tokens[index] == "{")
                    {
                        index++;
                        ParseBlock(tokens, ref index, variables, effectiveCond, spawner);
                    }
                }
                else if (t == "new")
                {
                    index++;
                    if (index >= tokens.Count) return;
                    string typeName = tokens[index++];
                    if (index < tokens.Count && tokens[index] == "(") index++;
                    var args = ParseArguments(tokens, ref index, variables, spawner);
                    if (index < tokens.Count && tokens[index] == ";") index++;
                    CreateNode(typeName, args, conditionNode, spawner);
                }
                else if (IsIdentifier(t) && index + 1 < tokens.Count && tokens[index + 1] == "(")
                {
                    string funcName = tokens[index++];
                    index++;
                    var args = ParseArguments(tokens, ref index, variables, spawner);
                    if (index < tokens.Count && tokens[index] == ";") index++;
                    CreateNode(funcName, args, conditionNode, spawner);
                }
                else if (IsIdentifier(t) && index + 1 < tokens.Count && tokens[index + 1] == "=")
                {
                    string name = tokens[index++];
                    index++;
                    Node valNode = ParseExpression(tokens, ref index, variables, spawner);

                    if (conditionNode != null && variables.ContainsKey(name))
                    {
                        var selectNode = new MathNode(MathNode.Operation.Select);
                        spawner(selectNode);
                        _engine.Connect(conditionNode, 0, selectNode, 0);
                        _engine.Connect(valNode, 0, selectNode, 1);
                        _engine.Connect(variables[name], 0, selectNode, 2);
                        variables[name] = selectNode;
                    }
                    else
                    {
                        variables[name] = valNode;
                    }

                    if (index < tokens.Count && tokens[index] == ";") index++;
                }
                else
                {
                    index++;
                }
            }
        }

        private bool IsIdentifier(string s) => s.Length > 0 && (char.IsLetter(s[0]) || s[0] == '_');

        private List<Node> ParseArguments(List<string> tokens, ref int index, Dictionary<string, Node> variables, Action<Node> spawner)
        {
            var args = new List<Node>();
            while (index < tokens.Count && tokens[index] != ")")
            {
                args.Add(ParseExpression(tokens, ref index, variables, spawner));
                if (index < tokens.Count && tokens[index] == ",") index++;
            }
            if (index < tokens.Count) index++;
            return args;
        }

        private Node ParseExpression(List<string> tokens, ref int index, Dictionary<string, Node> variables, Action<Node> spawner)
        {
            Node left = ParseTerm(tokens, ref index, variables, spawner);

            while (index < tokens.Count)
            {
                string op = tokens[index];
                if (op == "+" || op == "-" || op == "*" || op == "/" || op == ">" || op == "<")
                {
                    index++;
                    Node right = ParseTerm(tokens, ref index, variables, spawner);

                    Node opNode = op switch
                    {
                        "+" => new MathNode(MathNode.Operation.Add),
                        "-" => new MathNode(MathNode.Operation.Subtract),
                        "*" => new MathNode(MathNode.Operation.Multiply),
                        "/" => new MathNode(MathNode.Operation.Divide),
                        ">" => new LogicNode(LogicNode.LogicType.GreaterThan),
                        "<" => new LogicNode(LogicNode.LogicType.LessThan),
                        _ => new ConstantNode(0)
                    };

                    spawner(opNode);
                    _engine.Connect(left, 0, opNode, 0);
                    _engine.Connect(right, 0, opNode, 1);
                    left = opNode;
                }
                else break;
            }
            return left;
        }

        private Node ParseTerm(List<string> tokens, ref int index, Dictionary<string, Node> variables, Action<Node> spawner)
        {
            if (index >= tokens.Count) return new ConstantNode(0);
            string t = tokens[index++];
            if (float.TryParse(t, out float val))
            {
                var c = new ConstantNode(val);
                spawner(c);
                return c;
            }
            if (variables.ContainsKey(t)) return variables[t];
            if (t == "abs" && index < tokens.Count && tokens[index] == "(")
            {
                index++;
                Node arg = ParseExpression(tokens, ref index, variables, spawner);
                if (index < tokens.Count && tokens[index] == ")") index++;
                var absNode = new MathNode(MathNode.Operation.Abs);
                spawner(absNode);
                _engine.Connect(arg, 0, absNode, 0);
                return absNode;
            }
            if (t == "(")
            {
                Node n = ParseExpression(tokens, ref index, variables, spawner);
                if (index < tokens.Count && tokens[index] == ")") index++;
                return n;
            }
            return new ConstantNode(0);
        }

        private void CreateNode(string name, List<Node> args, Node? condition, Action<Node> spawner)
        {
            if (name == "beep")
            {
                var b = new BeepOutputNode();
                spawner(b);
                if (condition is not null) _engine.Connect(condition, 0, b, 0);
                else { var c = new ConstantNode(1); spawner(c); _engine.Connect(c, 0, b, 0); }
                if (args.Count > 0) _engine.Connect(args[0], 0, b, 1);
                if (args.Count > 1) _engine.Connect(args[1], 0, b, 2);
            }
            else if (name == "ColorNode")
            {
                var c = new ColorOutputNode();
                spawner(c);
                if (args.Count > 0) _engine.Connect(args[0], 0, c, 0);
                if (args.Count > 1) _engine.Connect(args[1], 0, c, 1);
                if (args.Count > 2) _engine.Connect(args[2], 0, c, 2);
            }
        }
    }
}
