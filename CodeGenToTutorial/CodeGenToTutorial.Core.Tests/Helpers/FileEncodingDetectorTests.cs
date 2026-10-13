using System.Text;

using CodeGenToTutorial.Core.Helpers;
using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Core.Tests.Helpers;

public class FileEncodingDetectorTests : IDisposable
{
    private readonly string _workingDirectory = Directory.CreateTempSubdirectory("CodeGenToTutorialTests_").FullName;

    public void Dispose() => Directory.Delete(_workingDirectory, recursive: true);

    private string WriteFile(byte[] bytes)
    {
        var path = Path.Combine(_workingDirectory, "file.txt");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Detect_FileDoesNotExist_ReturnsDefault()
    {
        var info = FileEncodingDetector.Detect(Path.Combine(_workingDirectory, "missing.txt"));

        Assert.Equal(FileEncodingInfo.Default, info);
    }

    [Fact]
    public void Detect_Utf8NoBomCrlf_DetectsNoBomAndCrlf()
    {
        var path = WriteFile(new UTF8Encoding(false).GetBytes("a\r\nb"));

        var info = FileEncodingDetector.Detect(path);

        Assert.Equal(Encoding.UTF8.CodePage, info.Encoding.CodePage);
        Assert.Empty(info.Encoding.GetPreamble());
        Assert.Equal("\r\n", info.NewLine);
    }

    [Fact]
    public void Detect_Utf8NoBomLf_DetectsLf()
    {
        var path = WriteFile(new UTF8Encoding(false).GetBytes("a\nb"));

        var info = FileEncodingDetector.Detect(path);

        Assert.Equal("\n", info.NewLine);
    }

    [Fact]
    public void Detect_Utf8Bom_DetectsBomPreamble()
    {
        var bomEncoding = new UTF8Encoding(true);
        var path = WriteFile(bomEncoding.GetPreamble().Concat(bomEncoding.GetBytes("a\nb")).ToArray());

        var info = FileEncodingDetector.Detect(path);

        Assert.NotEmpty(info.Encoding.GetPreamble());
        Assert.Equal(Encoding.UTF8.CodePage, info.Encoding.CodePage);
    }

    [Fact]
    public void Detect_Utf16LittleEndianBom_DetectsUnicodeEncoding()
    {
        var path = WriteFile(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("a\nb")).ToArray());

        var info = FileEncodingDetector.Detect(path);

        Assert.Equal(Encoding.Unicode.CodePage, info.Encoding.CodePage);
    }

    [Fact]
    public void Detect_Utf16BigEndianBom_DetectsBigEndianUnicodeEncoding()
    {
        var path = WriteFile(Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes("a\nb")).ToArray());

        var info = FileEncodingDetector.Detect(path);

        Assert.Equal(Encoding.BigEndianUnicode.CodePage, info.Encoding.CodePage);
    }

    [Fact]
    public void Detect_Utf32LittleEndianBom_DetectsUtf32LittleEndianNotUtf16()
    {
        var utf32Le = new UTF32Encoding(bigEndian: false, byteOrderMark: true);
        var path = WriteFile(utf32Le.GetPreamble().Concat(utf32Le.GetBytes("a\nb")).ToArray());

        var info = FileEncodingDetector.Detect(path);

        // The UTF-32 LE BOM ("FF FE 00 00") shares its first two bytes with the UTF-16 LE BOM
        // ("FF FE") - this is the disambiguation test.
        Assert.Equal(utf32Le.CodePage, info.Encoding.CodePage);
    }

    [Fact]
    public void Detect_Utf32BigEndianBom_DetectsUtf32BigEndian()
    {
        var utf32Be = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        var path = WriteFile(utf32Be.GetPreamble().Concat(utf32Be.GetBytes("a\nb")).ToArray());

        var info = FileEncodingDetector.Detect(path);

        Assert.Equal(utf32Be.CodePage, info.Encoding.CodePage);
    }

    [Fact]
    public void Detect_NoNewlines_ReturnsNullNewLine()
    {
        var path = WriteFile(new UTF8Encoding(false).GetBytes("single line, no newline"));

        var info = FileEncodingDetector.Detect(path);

        Assert.Null(info.NewLine);
    }

    [Fact]
    public void Detect_EmptyFile_ReturnsNullNewLine()
    {
        var path = WriteFile(Array.Empty<byte>());

        var info = FileEncodingDetector.Detect(path);

        Assert.Null(info.NewLine);
    }

    [Fact]
    public void Detect_MixedLineEndings_PrefersCrlfWhenAnyCrlfPresent()
    {
        var path = WriteFile(new UTF8Encoding(false).GetBytes("a\r\nb\nc"));

        var info = FileEncodingDetector.Detect(path);

        Assert.Equal("\r\n", info.NewLine);
    }

    [Fact]
    public void Detect_PathIsDirectory_ReturnsDefaultWithoutThrowing()
    {
        // File.Exists is false for a directory, so this exercises the "doesn't exist" branch
        // rather than Detect's own IOException/UnauthorizedAccessException handling - see
        // Detect_UnreadableFile_ReturnsDefaultWithoutThrowing for that.
        var info = FileEncodingDetector.Detect(_workingDirectory);

        Assert.Equal(FileEncodingInfo.Default, info);
    }

    [Fact]
    public void Detect_UnreadableFile_ReturnsDefaultWithoutThrowing()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            // Windows ACLs and root both make a reliable "permission denied" repro impractical
            // here, same caveat as FileChangeApplyServiceTests' permission test.
            return;
        }

        var path = WriteFile(new UTF8Encoding(false).GetBytes("a\nb"));
        File.SetUnixFileMode(path, UnixFileMode.None);

        try
        {
            var info = FileEncodingDetector.Detect(path);

            Assert.Equal(FileEncodingInfo.Default, info);
        }
        finally
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
