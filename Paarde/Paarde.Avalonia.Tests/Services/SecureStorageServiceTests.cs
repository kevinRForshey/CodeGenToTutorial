using Microsoft.Extensions.Options;

using Paarde.Avalonia.Models;
using Paarde.Avalonia.Services;
using Paarde.Avalonia.Tests.Fakes;

namespace Paarde.Avalonia.Tests.Services;

// Only exercises the AES fallback path (this sandbox is Linux) - the DPAPI branch is Windows-only
// and can't be run here, see SecureStorageService's own comment on that tradeoff.
//
// GetOrCreateLocalKey writes its key file via plain File.* calls rather than through IFileService, so
// it still touches real disk even with a fake IFileService - a uniquely named folder under the real
// ApplicationData directory (cleaned up in Dispose) keeps that from colliding with or polluting a
// real app installation's own SecureSettings.key.
public class SecureStorageServiceTests : IDisposable
{
    private readonly string _relativeFolder = Path.Combine("Paarde.Avalonia.Tests", Guid.NewGuid().ToString());

    private SecureStorageService CreateSut(FakeFileService fileService) =>
        new(fileService, Options.Create(new LocalSettingsOptions { ApplicationDataFolder = _relativeFolder }));

    public void Dispose()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _relativeFolder);

        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenReadAsync_RoundTripsTheOriginalValue()
    {
        var sut = CreateSut(new FakeFileService());

        await sut.SaveAsync("AnthropicApiKey", "sk-ant-super-secret");

        Assert.Equal("sk-ant-super-secret", await sut.ReadAsync("AnthropicApiKey"));
    }

    [Fact]
    public async Task ReadAsync_KeyNeverSaved_ReturnsNull()
    {
        var sut = CreateSut(new FakeFileService());

        Assert.Null(await sut.ReadAsync("AnthropicApiKey"));
    }

    [Fact]
    public async Task SaveAsync_PersistsCiphertextRatherThanThePlaintextValue()
    {
        var fileService = new FakeFileService();
        var sut = CreateSut(fileService);

        await sut.SaveAsync("AnthropicApiKey", "sk-ant-super-secret");

        var applicationDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), _relativeFolder);
        var persisted = fileService.Read<IDictionary<string, string>>(applicationDataFolder, "SecureSettings.dat");
        Assert.DoesNotContain("sk-ant-super-secret", persisted!["AnthropicApiKey"]);
    }

    [Fact]
    public async Task SaveAsync_DifferentInstanceSharingTheSameFileService_CanReadTheValueBack()
    {
        var fileService = new FakeFileService();
        await CreateSut(fileService).SaveAsync("AnthropicApiKey", "sk-ant-super-secret");

        var secondInstance = CreateSut(fileService);

        Assert.Equal("sk-ant-super-secret", await secondInstance.ReadAsync("AnthropicApiKey"));
    }
}
