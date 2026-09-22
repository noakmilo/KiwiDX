using System.Buffers.Binary;
namespace KiwiDX;

// Independent wire implementation. Protocol references and field layouts: docs/SPYSERVER.md.
// All integers are little endian. Commands: uint32 type, uint32 body size, body.
// Messages: uint32 version, type (upper 16 bits = gain), stream, sequence, body size.
internal static class SpyServerProtocol
{
    internal const uint Version = 0x020006a4;
    internal const int MaxBody = 1 << 20;
    internal static uint U32(ReadOnlySpan<byte> bytes, int offset = 0) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    internal static (uint Type, uint Stream, uint Sequence, int Size) Header(ReadOnlySpan<byte> data)
    {
        if (data.Length != 20) throw new InvalidDataException("Incomplete SpyServer header.");
        if ((U32(data) >> 16) != (Version >> 16)) throw new InvalidDataException("Unsupported SpyServer protocol version.");
        uint length = U32(data, 16);
        if (length > MaxBody) throw new InvalidDataException("SpyServer packet exceeds the 1 MB limit.");
        return (U32(data, 4), U32(data, 8), U32(data, 12), (int)length);
    }
    internal static byte[] Command(uint command, params uint[] words)
    {
        var b = new byte[8 + words.Length * 4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, command);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), (uint)(b.Length - 8));
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8 + i * 4), words[i]);
        return b;
    }
    internal static int DecodeIq(uint type, ReadOnlySpan<byte> body, Span<float> iq)
    {
        int bytes = (type & 65535) switch { 100 => 1, 101 => 2, 102 => 3, 103 => 4, _ => throw new InvalidDataException("Unsupported SpyServer IQ format.") };
        if (body.Length % (2 * bytes) != 0 || iq.Length < body.Length / bytes) throw new InvalidDataException("Invalid SpyServer IQ sample count.");
        double gain = Math.Pow(10, Math.Min(type >> 16, 200) / 20.0);
        int count = body.Length / bytes;
        for (int i = 0, p = 0; i < count; i++, p += bytes)
        {
            double value = bytes switch {
                1 => (body[p] - 128) / (128 * gain),
                2 => BinaryPrimitives.ReadInt16LittleEndian(body[p..]) / (32768 * gain),
                3 => ((body[p] | body[p+1] << 8 | body[p+2] << 16) << 8 >> 8) / (8388608 * gain),
                _ => BinaryPrimitives.ReadSingleLittleEndian(body[p..]) * gain
            };
            iq[i] = double.IsFinite(value) ? (float)Math.Clamp(value, -16, 16) : 0;
        }
        return count;
    }
}

internal sealed record SpyServerDevice(uint Type, uint Serial, uint SampleRate, uint Bandwidth, int Stages,
    uint MaximumGain, uint MinimumFrequency, uint MaximumFrequency, int MinimumDecimation, uint ForcedFormat)
{
    public string Name => Type switch { 1 => "Airspy One / Mini / R2", 2 => "Airspy HF+", 3 => "RTL-SDR", _ => "SpyServer receiver" };
    internal static SpyServerDevice Parse(ReadOnlySpan<byte> b)
    {
        if (b.Length < 48) throw new InvalidDataException("Incomplete SpyServer device information.");
        var d = new SpyServerDevice(SpyServerProtocol.U32(b), SpyServerProtocol.U32(b,4), SpyServerProtocol.U32(b,8),
            SpyServerProtocol.U32(b,12), (int)SpyServerProtocol.U32(b,16), SpyServerProtocol.U32(b,24),
            SpyServerProtocol.U32(b,28), SpyServerProtocol.U32(b,32), (int)SpyServerProtocol.U32(b,40), SpyServerProtocol.U32(b,44));
        if (d.SampleRate is < 8000 or > 100_000_000 || d.Bandwidth == 0 || d.Bandwidth > d.SampleRate ||
            d.Stages is < 1 or > 24 || d.MinimumDecimation < 0 || d.MinimumDecimation >= d.Stages || d.MinimumFrequency >= d.MaximumFrequency)
            throw new InvalidDataException("Invalid SpyServer device capabilities.");
        return d;
    }
}
