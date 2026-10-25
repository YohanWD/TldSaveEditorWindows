namespace TldSave.Core;

/// <summary>
/// LZF compression (port of liblzf), the format used by The Long Dark save files.
/// Raw stream, no header.
/// </summary>
public static class Lzf
{
    private const int HashLog = 14;
    private const int HashSize = 1 << HashLog;
    private const int MaxLiteral = 1 << 5;          // 32
    private const int MaxOffset = 1 << 13;          // 8192
    private const int MaxReference = (1 << 8) + (1 << 3); // 264

    public static byte[] Decompress(ReadOnlySpan<byte> input)
    {
        var output = new byte[Math.Max(input.Length * 4, 64)];
        int ip = 0, op = 0;

        while (ip < input.Length)
        {
            int ctrl = input[ip++];

            if (ctrl < 32)
            {
                // Literal run of ctrl + 1 bytes
                int length = ctrl + 1;
                if (ip + length > input.Length) throw new InvalidDataException("LZF: literal run past end of input");
                EnsureCapacity(ref output, op + length);
                input.Slice(ip, length).CopyTo(output.AsSpan(op));
                ip += length;
                op += length;
            }
            else
            {
                // Back reference: copy (length + 2) bytes from earlier output
                int length = ctrl >> 5;
                int reference = op - ((ctrl & 0x1F) << 8) - 1;
                if (length == 7) length += input[ip++];
                reference -= input[ip++];
                length += 2;

                if (reference < 0) throw new InvalidDataException("LZF: back reference before start of output");
                EnsureCapacity(ref output, op + length);
                // Byte by byte on purpose: source and destination may overlap
                for (int i = 0; i < length; i++) output[op++] = output[reference++];
            }
        }

        Array.Resize(ref output, op);
        return output;
    }

    public static byte[] Compress(ReadOnlySpan<byte> input)
    {
        var output = new byte[input.Length + input.Length / MaxLiteral + 16];
        var hashTable = new int[HashSize];
        Array.Fill(hashTable, -1);

        int ip = 0;
        int op = 1; // reserve the first literal-length byte
        int literals = 0;

        while (ip < input.Length - 2)
        {
            int hash = (input[ip] << 16) | (input[ip + 1] << 8) | input[ip + 2];
            int slot = ((hash >> (3 * 8 - HashLog)) - hash * 5) & (HashSize - 1);
            int reference = hashTable[slot];
            hashTable[slot] = ip;

            int offset = ip - reference - 1;
            if (reference >= 0
                && offset < MaxOffset
                && input[reference] == input[ip]
                && input[reference + 1] == input[ip + 1]
                && input[reference + 2] == input[ip + 2])
            {
                int maxLength = Math.Min(input.Length - ip - 2, MaxReference);
                int length = 2;
                do length++; while (length < maxLength && input[reference + length] == input[ip + length]);

                // Close the pending literal run (or drop its reserved byte if empty)
                if (literals == 0) op--;
                else output[op - literals - 1] = (byte)(literals - 1);

                length -= 2;
                if (length < 7)
                {
                    output[op++] = (byte)((offset >> 8) + (length << 5));
                }
                else
                {
                    output[op++] = (byte)((offset >> 8) + (7 << 5));
                    output[op++] = (byte)(length - 7);
                }
                output[op++] = (byte)offset;

                literals = 0;
                op++; // reserve the next literal-length byte
                ip += length + 2;
            }
            else
            {
                AddLiteral(input[ip++]);
            }
        }

        while (ip < input.Length) AddLiteral(input[ip++]);

        if (literals == 0) op--;
        else output[op - literals - 1] = (byte)(literals - 1);

        Array.Resize(ref output, op);
        return output;

        void AddLiteral(byte value)
        {
            literals++;
            output[op++] = value;
            if (literals == MaxLiteral)
            {
                output[op - literals - 1] = MaxLiteral - 1;
                literals = 0;
                op++;
            }
        }
    }

    private static void EnsureCapacity(ref byte[] buffer, int needed)
    {
        if (needed <= buffer.Length) return;
        Array.Resize(ref buffer, Math.Max(needed, buffer.Length * 2));
    }
}
