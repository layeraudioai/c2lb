using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ToyConEngine
{
    public sealed class MidiImportResult
    {
        public List<MidiTrack> Tracks { get; } = new();
        public int TickDivision { get; set; }
        public int TotalNoteCount => Tracks.Count == 0 ? 0 : Tracks.Sum(track => track.Notes.Count);
    }

    public sealed class MidiTrack
    {
        public string Name { get; set; } = "Track";
        public int Channel { get; set; }
        public int Program { get; set; }
        public bool IsPercussion { get; set; }
        public List<MidiNoteEvent> Notes { get; } = new();
    }

    public sealed class MidiNoteEvent
    {
        public double TimeSeconds { get; set; }
        public int NoteNumber { get; set; }
        public int Velocity { get; set; }
        public int Program { get; set; }
        public bool IsPercussion { get; set; }
    }

    public static class MidiImporter
    {
        public static MidiImportResult? ParseFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

                var bytes = File.ReadAllBytes(path);
                if (bytes.Length < 14) return null;
                if (Encoding.ASCII.GetString(bytes, 0, 4) != "MThd") return null;

                int headerLength = ReadInt32(bytes, 4);
                if (headerLength < 6) return null;

                int format = ReadInt16(bytes, 8);
                int trackCount = ReadInt16(bytes, 10);
                int division = ReadInt16(bytes, 12);

                var result = new MidiImportResult { TickDivision = division < 0 ? 480 : division };
                int offset = 14;
                for (int i = 0; i < trackCount; i++)
                {
                    if (offset + 8 > bytes.Length) break;
                    if (Encoding.ASCII.GetString(bytes, offset, 4) != "MTrk") break;

                    int chunkLength = ReadInt32(bytes, offset + 4);
                    int dataStart = offset + 8;
                    int dataEnd = dataStart + chunkLength;
                    if (dataEnd > bytes.Length) dataEnd = bytes.Length;
                    if (dataEnd < dataStart) break;

                    var track = ParseTrack(bytes, dataStart, dataEnd, division, i + 1);
                    if (track != null) result.Tracks.Add(track);
                    offset = dataEnd;
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        private static MidiTrack? ParseTrack(byte[] bytes, int start, int end, int division, int trackIndex)
        {
            var track = new MidiTrack
            {
                Name = $"Track {trackIndex}",
                Program = 0,
                Channel = 0,
                IsPercussion = false
            };

            int offset = start;
            int runningStatus = -1;
            int absoluteTicks = 0;
            double currentTime = 0.0;
            int tempoUsPerQuarter = 500000;

            while (offset < end)
            {
                int delta = ReadVarInt(bytes, ref offset);
                absoluteTicks += delta;
                if (division != 0)
                {
                    currentTime += delta * tempoUsPerQuarter / 1000000.0 / division;
                }

                if (offset >= end) break;
                byte eventByte = bytes[offset++];

                if (eventByte == 0xFF)
                {
                    if (offset >= end) break;
                    byte metaType = bytes[offset++];
                    int metaLength = ReadVarInt(bytes, ref offset);
                    if (offset + metaLength > end) metaLength = Math.Max(0, end - offset);
                    var payload = new byte[metaLength];
                    Array.Copy(bytes, offset, payload, 0, metaLength);
                    offset += metaLength;

                    if (metaType == 0x03)
                    {
                        track.Name = Encoding.ASCII.GetString(payload).TrimEnd('\0');
                    }
                    else if (metaType == 0x51 && metaLength >= 3)
                    {
                        tempoUsPerQuarter = (payload[0] << 16) | (payload[1] << 8) | payload[2];
                    }

                    continue;
                }

                if (eventByte == 0xF0 || eventByte == 0xF7)
                {
                    int length = ReadVarInt(bytes, ref offset);
                    if (offset + length > end) length = Math.Max(0, end - offset);
                    offset += length;
                    continue;
                }

                int status;
                if (eventByte >= 0x80)
                {
                    status = eventByte;
                    runningStatus = status;
                }
                else
                {
                    status = runningStatus;
                    offset--;
                    if (status < 0) continue;
                }

                int messageType = status & 0xF0;
                int channel = status & 0x0F;
                if (offset >= end) break;

                byte data1 = bytes[offset++];
                byte data2 = 0;
                if (messageType != 0xC0 && messageType != 0xD0)
                {
                    if (offset >= end) break;
                    data2 = bytes[offset++];
                }

                switch (messageType)
                {
                    case 0xC0:
                        track.Program = data1;
                        break;
                    case 0x90:
                        if (data2 > 0)
                        {
                            track.Notes.Add(new MidiNoteEvent
                            {
                                TimeSeconds = currentTime,
                                NoteNumber = data1,
                                Velocity = data2,
                                Program = track.Program,
                                IsPercussion = channel == 9
                            });
                        }
                        break;
                }

                track.Channel = channel;
                track.IsPercussion = channel == 9;
            }

            return track.Notes.Count > 0 ? track : null;
        }

        private static int ReadVarInt(byte[] bytes, ref int offset)
        {
            int value = 0;
            while (offset < bytes.Length)
            {
                byte b = bytes[offset++];
                value = (value << 7) | (b & 0x7F);
                if ((b & 0x80) == 0) break;
            }
            return value;
        }

        private static int ReadInt16(byte[] bytes, int offset)
        {
            return (bytes[offset] << 8) | bytes[offset + 1];
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }
    }
}
