using System.Text;

namespace TldSave.Core.Tests;

public class LzfTests
{
    public static IEnumerable<object[]> Inputs()
    {
        var random = new Random(42);
        var noise = new byte[100_000];
        random.NextBytes(noise);

        yield return [Array.Empty<byte>()];
        yield return [new byte[] { 7 }];
        yield return [noise];                                                       // incompressible
        yield return [Enumerable.Repeat((byte)'A', 100_000).ToArray()];             // long back references
        yield return [Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("{\"m_UnitsProxy\":5},", 5000)))];
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Compress_then_decompress_gives_the_same_bytes(byte[] input)
    {
        Assert.Equal(input, Lzf.Decompress(Lzf.Compress(input)));
    }

    [Fact]
    public void Repetitive_data_is_actually_compressed()
    {
        var input = Enumerable.Repeat((byte)'A', 100_000).ToArray();
        Assert.True(Lzf.Compress(input).Length < input.Length / 10);
    }

    [Theory]
    [InlineData(new byte[] { 0, (byte)'A', 0x20 })]          // back reference missing its offset byte
    [InlineData(new byte[] { 0, (byte)'A', 0xE0, 5 })]       // long back reference missing a byte
    [InlineData(new byte[] { 5, (byte)'A' })]                // literal run longer than the input
    [InlineData(new byte[] { 0x20, 0 })]                     // back reference before the start of the output
    public void Truncated_or_corrupt_input_gives_InvalidDataException(byte[] input)
    {
        Assert.Throws<InvalidDataException>(() => Lzf.Decompress(input));
    }

    [Fact]
    public void Decompression_bomb_is_rejected_instead_of_filling_memory()
    {
        // 1 literal byte, then ~1.1M max-length back references: ~3 MB that would expand to ~276 MB
        var bomb = new List<byte> { 0, (byte)'A' };
        for (var i = 0; i < 1_100_000; i++) bomb.AddRange([0xE0, 255, 0]);

        var error = Assert.Throws<InvalidDataException>(() => Lzf.Decompress(bomb.ToArray()));
        Assert.Contains("exceeds", error.Message);
    }
}
