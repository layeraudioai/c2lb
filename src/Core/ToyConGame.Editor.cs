using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace ToyConEngine
{
    public partial class ToyConGame
    {
        private void DeleteNode(Node node)
        {
            _engine.Nodes.Remove(node);
            _nodeRects.Remove(node);
            if (_inspectedNode == node) _inspectedNode = null;
            _selectedNodes.Remove(node);

            foreach (var n in _engine.Nodes)
            {
                foreach (var input in n.Inputs)
                {
                    input.ConnectedSources.RemoveAll(s => s.ParentNode == node);
                }
            }
        }

        private void DeleteSelectedNodes()
        {
            var nodesToDelete = new List<Node>(_selectedNodes);
            foreach (var node in nodesToDelete)
            {
                DeleteNode(node);
            }
            _selectedNodes.Clear();
        }

        private Node CloneNode(Node original)
        {
            Node? clone = null;
            if (original is ConstantNode c) clone = new ConstantNode(c.StoredValue);
            else if (original is MathNode m) clone = new MathNode(m.Op);
            else if (original is LogicNode l) clone = new LogicNode(l.Type);
            else if (original is TimerNode) clone = new TimerNode();
            else if (original is CounterNode cnt) { clone = new CounterNode(); ((CounterNode)clone).Value = cnt.Value; }
            else if (original is RandomNode) clone = new RandomNode();
            else if (original is ButtonNode b) { clone = new ButtonNode(); ((ButtonNode)clone).IsToggle = b.IsToggle; }
            else if (original is KeyNode k) { clone = new KeyNode(); ((KeyNode)clone).Key = k.Key; ((KeyNode)clone).Name = k.Name; }
            else if (original is CursorNode) clone = new CursorNode();
            else if (original is ColorOutputNode) clone = new ColorOutputNode();
            else if (original is BeepOutputNode bp) { clone = new BeepOutputNode(); ((BeepOutputNode)clone).SoundName = bp.SoundName; }
            else if (original is ScreenNode) clone = new ScreenNode();
            else if (original is ScriptImporterNode s) { clone = new ScriptImporterNode(); ((ScriptImporterNode)clone).Script = s.Script; }
            else if (original is MidiImporterNode midi) { clone = new MidiImporterNode(); ((MidiImporterNode)clone).MidiPath = midi.MidiPath; ((MidiImporterNode)clone).LastImportMessage = midi.LastImportMessage; }

            return clone ?? throw new InvalidOperationException($"Unsupported node type: {original.GetType().Name}");
        }

        private void CopyNodes()
        {
            _clipboardNodes.Clear();
            _clipboardConnections.Clear();

            if (_selectedNodes.Count == 0) return;

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (var node in _selectedNodes)
            {
                if (_nodeRects.TryGetValue(node, out Rectangle r))
                {
                    if (r.X < minX) minX = r.X;
                    if (r.Y < minY) minY = r.Y;
                }
            }

            var nodeMap = new Dictionary<Node, int>();
            for (int i = 0; i < _selectedNodes.Count; i++)
            {
                var original = _selectedNodes[i];
                var clone = CloneNode(original);
                Point offset = Point.Zero;
                if (_nodeRects.TryGetValue(original, out Rectangle r))
                    offset = new Point(r.X - minX, r.Y - minY);

                _clipboardNodes.Add((clone, offset));
                nodeMap[original] = _clipboardNodes.Count - 1;
            }

            for (int i = 0; i < _selectedNodes.Count; i++)
            {
                var original = _selectedNodes[i];
                if (!nodeMap.ContainsKey(original)) continue;

                for (int inputIdx = 0; inputIdx < original.Inputs.Count; inputIdx++)
                {
                    var input = original.Inputs[inputIdx];
                    foreach (var source in input.ConnectedSources)
                    {
                        if (source.ParentNode is not null && nodeMap.ContainsKey(source.ParentNode))
                        {
                            int sourceNodeIdx = nodeMap[source.ParentNode];
                            int sourceOutputIdx = source.ParentNode.Outputs.IndexOf(source);

                            _clipboardConnections.Add(new ConnectionData
                            {
                                TargetNodeIdx = nodeMap[original],
                                TargetInputIdx = inputIdx,
                                SourceNodeIdx = sourceNodeIdx,
                                SourceOutputIdx = sourceOutputIdx
                            });
                        }
                    }
                }
            }
        }

        private void PasteNodes()
        {
            if (_clipboardNodes.Count == 0) return;

            _selectedNodes.Clear();
            var newNodes = new List<Node>();
            Point mousePos = Mouse.GetState().Position;
            int spread = 24;

            for (int i = 0; i < _clipboardNodes.Count; i++)
            {
                var entry = _clipboardNodes[i];
                var newNode = CloneNode(entry.Node);
                newNodes.Add(newNode);
                int offsetX = (i % 6) * spread;
                int offsetY = (i / 6) * spread;
                SpawnNodeAt(newNode, mousePos.X + entry.Offset.X + offsetX, mousePos.Y + entry.Offset.Y + offsetY);
                _selectedNodes.Add(newNode);
            }

            foreach (var conn in _clipboardConnections)
            {
                if (conn.TargetNodeIdx < newNodes.Count && conn.SourceNodeIdx < newNodes.Count)
                {
                    var target = newNodes[conn.TargetNodeIdx];
                    var source = newNodes[conn.SourceNodeIdx];

                    if (conn.TargetInputIdx < target.Inputs.Count && conn.SourceOutputIdx < source.Outputs.Count)
                    {
                        _engine.Connect(source, conn.SourceOutputIdx, target, conn.TargetInputIdx);
                    }
                }
            }
        }

        private void SpawnNode(Node node)
        {
            var mousePos = Mouse.GetState().Position;
            SpawnNodeAt(node, mousePos.X, mousePos.Y);
        }

        private void SpawnNodeAt(Node node, int x, int y)
        {
            _engine.Nodes.Add(node);
            int width = 100;
            int height = 60;
            if (node.Inputs.Count + node.Outputs.Count > 2)
            {
                width = 120;
                height = 80;
            }
            if (node is ScreenNode)
            {
                width = 140;
                height = 140;
            }

            var rect = new Rectangle(x, y, width, height);
            ClampNodeRect(ref rect);
            _nodeRects[node] = rect;
        }

        private void ClampNodeRect(ref Rectangle rect)
        {
            int width = ClientBounds.Width > 0 ? ClientBounds.Width : 800;
            int height = ClientBounds.Height > 0 ? ClientBounds.Height : 600;
            int minX = 5;
            int minY = _uiBarRect.Height + 10;
            int maxX = Math.Max(minX, width - rect.Width - 5);
            int maxY = Math.Max(minY, height - rect.Height - 5);
            rect.X = Math.Clamp(rect.X, minX, maxX);
            rect.Y = Math.Clamp(rect.Y, minY, maxY);
        }

        private Vector2 GetInputPosition(Node node, int slotIndex)
        {
            var rect = _nodeRects[node];
            float y = rect.Y + (rect.Height * (slotIndex + 1) / (float)(node.Inputs.Count + 1));
            return new Vector2(rect.Left, y);
        }

        private Vector2 GetOutputPosition(Node node, int slotIndex)
        {
            var rect = _nodeRects[node];
            float y = rect.Y + (rect.Height * (slotIndex + 1) / (float)(node.Outputs.Count + 1));
            return new Vector2(rect.Right, y);
        }

        private float GetDistanceFromLineSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.LengthSquared();
            if (l2 == 0) return (p - a).Length();
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0f, 1f);
            Vector2 projection = a + t * ab;
            return (p - projection).Length();
        }

        private void TryConnect(Node sourceNode, int sourceIndex, Node targetNode, int targetIndex)
        {
            if (sourceNode == null || targetNode == null) return;
            if (sourceNode == targetNode && sourceIndex == targetIndex) return;
            if (sourceIndex < 0 || sourceIndex >= sourceNode.Outputs.Count) return;
            if (targetIndex < 0 || targetIndex >= targetNode.Inputs.Count) return;

            _engine.Connect(sourceNode, sourceIndex, targetNode, targetIndex);
        }
    }
}
