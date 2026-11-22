using System.Text;
using Google.Apis.Auth.OAuth2.Responses;
using Newtonsoft.Json;
using Utu.Core;
using Utu.YouTubeApi;

namespace Utu.Tests;

[TestClass]
public sealed class ProtectedTokenStoreTests
{
    private const string AccessToken = "ya29.a0AfH6SMB-secret-access-token";
    private const string RefreshToken = "1//0g-secret-refresh-token";

    [TestMethod]
    public async Task A_stored_token_is_encrypted_on_disk()
    {
        RequireWindows();

        using var folder = new TempFolder();
        var store = new ProtectedTokenStore(folder.Path);

        await store.StoreAsync("default", Token());

        var path = folder["default"];
        var bytes = File.ReadAllBytes(path);

        Assert.IsFalse(Encoding.UTF8.GetString(bytes).Contains(AccessToken), "the token was written in clear text");
        Assert.AreNotEqual((byte)'{', bytes[0], "the token file is plain JSON");

        var loaded = await store.GetAsync<TokenResponse>("default");

        Assert.IsNotNull(loaded);
        Assert.AreEqual(AccessToken, loaded.AccessToken);
        Assert.AreEqual(RefreshToken, loaded.RefreshToken);
        Assert.AreEqual(3600L, loaded.ExpiresInSeconds);
    }

    [TestMethod]
    public async Task A_clear_text_token_of_an_earlier_version_is_protected_when_it_is_read()
    {
        RequireWindows();

        using var folder = new TempFolder();
        var store = new ProtectedTokenStore(folder.Path);
        var path = folder.WriteFile("default", JsonConvert.SerializeObject(Token()));

        var loaded = await store.GetAsync<TokenResponse>("default");

        Assert.IsNotNull(loaded);
        Assert.AreEqual(AccessToken, loaded.AccessToken);

        var bytes = File.ReadAllBytes(path);
        Assert.IsFalse(Encoding.UTF8.GetString(bytes).Contains(AccessToken), "the clear text token was left on disk");

        var reloaded = await store.GetAsync<TokenResponse>("default");

        Assert.IsNotNull(reloaded);
        Assert.AreEqual(AccessToken, reloaded.AccessToken);
    }

    [TestMethod]
    public async Task A_missing_token_is_returned_as_default()
    {
        using var folder = new TempFolder();
        var store = new ProtectedTokenStore(folder.Path);

        Assert.IsNull(await store.GetAsync<TokenResponse>("default"));
        Assert.AreEqual(0, Directory.EnumerateFiles(folder.Path).Count());
    }

    [TestMethod]
    public async Task Deleting_a_token_removes_its_file()
    {
        RequireWindows();

        using var folder = new TempFolder();
        var store = new ProtectedTokenStore(folder.Path);
        await store.StoreAsync("default", Token());

        await store.DeleteAsync<TokenResponse>("default");

        Assert.IsFalse(File.Exists(folder["default"]));
        await store.DeleteAsync<TokenResponse>("default");
    }

    [TestMethod]
    public async Task Clearing_removes_the_tokens_of_all_users()
    {
        RequireWindows();

        using var folder = new TempFolder();
        var store = new ProtectedTokenStore(folder.Path);
        await store.StoreAsync("default", Token());
        await store.StoreAsync("second", Token());

        await store.ClearAsync();

        Assert.AreEqual(0, Directory.EnumerateFiles(folder.Path).Count());
        await store.ClearAsync();
    }

    [TestMethod]
    public async Task A_damaged_token_reports_an_error()
    {
        using var folder = new TempFolder();
        var store = new ProtectedTokenStore(folder.Path);
        folder.WriteFile("default", "not a token");

        var exception = await Assert.ThrowsExactlyAsync<UtuException>(
            () => store.GetAsync<TokenResponse>("default"));

        StringAssert.Contains(exception.Message, folder["default"]);
        StringAssert.Contains(exception.Message, "Delete the file");
    }

    [TestMethod]
    public async Task A_name_that_is_not_a_plain_file_name_is_rejected()
    {
        using var folder = new TempFolder();
        var store = new ProtectedTokenStore(folder.Path);

        var exception = await Assert.ThrowsExactlyAsync<UtuException>(
            () => store.StoreAsync("sub/default", Token()));

        StringAssert.Contains(exception.Message, "'sub/default'");
    }

    private static TokenResponse Token() => new()
    {
        AccessToken = AccessToken,
        TokenType = "Bearer",
        RefreshToken = RefreshToken,
        ExpiresInSeconds = 3600,
    };

    private static void RequireWindows() =>
        Assert.IsTrue(OperatingSystem.IsWindows(), "the data protection API is Windows only");
}