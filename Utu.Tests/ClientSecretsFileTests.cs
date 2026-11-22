using Utu.Core;
using Utu.YouTubeApi;

namespace Utu.Tests;

[TestClass]
public sealed class ClientSecretsFileTests
{
    private const string SecretsJson = """
        {
          "installed": {
            "client_id": "id.apps.googleusercontent.com",
            "client_secret": "secret",
            "redirect_uris": ["http://localhost"],
            "auth_uri": "https://accounts.google.com/o/oauth2/auth",
            "token_uri": "https://oauth2.googleapis.com/token"
          }
        }
        """;

    [TestMethod]
    public void The_suffix_is_added_to_a_plain_path_and_kept_on_a_protected_one()
    {
        var plain = @"C:\home\.utu\client_secrets.json";

        Assert.AreEqual(plain + ClientSecretsFile.ProtectedSuffix, ClientSecretsFile.ProtectedPath(plain));
        Assert.AreEqual(plain, ClientSecretsFile.PlainPath(plain));
        Assert.AreEqual(plain + ClientSecretsFile.ProtectedSuffix, ClientSecretsFile.ProtectedPath(plain + ".protected"));
        Assert.AreEqual(plain, ClientSecretsFile.PlainPath(plain + ".protected"));
    }

    [TestMethod]
    public void The_suffix_is_recognized_case_insensitively()
    {
        var protectedPath = @"C:\home\.utu\client_secrets.json.PROTECTED";

        Assert.AreEqual(protectedPath, ClientSecretsFile.ProtectedPath(protectedPath));
        Assert.AreEqual(@"C:\home\.utu\client_secrets.json", ClientSecretsFile.PlainPath(protectedPath));
    }

    [TestMethod]
    public void Protecting_a_plain_file_creates_the_protected_file_and_deletes_the_plain_one()
    {
        using var folder = new TempFolder();
        var plain = folder.WriteFile("client_secrets.json", SecretsJson);

        var protectedPath = ClientSecretsFile.EnsureProtected(plain);

        Assert.AreEqual(plain + ClientSecretsFile.ProtectedSuffix, protectedPath);
        Assert.IsTrue(File.Exists(protectedPath));
        Assert.IsFalse(File.Exists(plain));
        CollectionAssert.AreNotEqual(
            File.ReadAllBytes(protectedPath),
            System.Text.Encoding.UTF8.GetBytes(SecretsJson));
    }

    [TestMethod]
    public void An_existing_protected_file_is_kept_when_there_is_no_plain_file()
    {
        using var folder = new TempFolder();
        var plain = folder.WriteFile("client_secrets.json", SecretsJson);
        var protectedPath = ClientSecretsFile.EnsureProtected(plain);
        var before = File.ReadAllBytes(protectedPath);

        var again = ClientSecretsFile.EnsureProtected(plain);

        Assert.AreEqual(protectedPath, again);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(again));
    }

    [TestMethod]
    public void A_new_plain_file_replaces_an_outdated_protected_file()
    {
        using var folder = new TempFolder();
        var plain = folder["client_secrets.json"];
        File.WriteAllText(plain, SecretsJson);
        var protectedPath = ClientSecretsFile.EnsureProtected(plain);
        File.WriteAllText(protectedPath, "outdated");

        folder.WriteFile("client_secrets.json", SecretsJson);
        var again = ClientSecretsFile.EnsureProtected(plain);

        Assert.AreEqual(protectedPath, again);
        Assert.IsFalse(File.Exists(plain));
        Assert.AreEqual("secret", ClientSecretsFile.Load(plain).ClientSecret);
    }

    [TestMethod]
    public void Exists_reports_plain_and_protected_files()
    {
        using var folder = new TempFolder();
        var plain = folder["client_secrets.json"];

        Assert.IsFalse(ClientSecretsFile.Exists(plain));

        File.WriteAllText(plain, SecretsJson);
        Assert.IsTrue(ClientSecretsFile.Exists(plain));

        File.Delete(plain);
        Assert.IsFalse(ClientSecretsFile.Exists(plain));

        File.WriteAllText(plain + ClientSecretsFile.ProtectedSuffix, "protected");
        Assert.IsTrue(ClientSecretsFile.Exists(plain));
    }

    [TestMethod]
    public void Loading_reads_secrets_that_were_protected_on_demand()
    {
        using var folder = new TempFolder();
        var plain = folder.WriteFile("client_secrets.json", SecretsJson);

        var secrets = ClientSecretsFile.Load(plain);

        Assert.AreEqual("id.apps.googleusercontent.com", secrets.ClientId);
        Assert.AreEqual("secret", secrets.ClientSecret);
        Assert.IsTrue(File.Exists(ClientSecretsFile.ProtectedPath(plain)));
        Assert.IsFalse(File.Exists(plain));
    }

    [TestMethod]
    public void Loading_a_protected_path_works_as_well()
    {
        using var folder = new TempFolder();
        var plain = folder.WriteFile("client_secrets.json", SecretsJson);

        var secrets = ClientSecretsFile.Load(ClientSecretsFile.ProtectedPath(plain));

        Assert.AreEqual("secret", secrets.ClientSecret);
        Assert.IsFalse(File.Exists(plain));
    }

    [TestMethod]
    public void Loading_throws_when_there_are_no_secrets()
    {
        using var folder = new TempFolder();

        var exception = Assert.ThrowsExactly<UtuException>(
            () => ClientSecretsFile.Load(folder["client_secrets.json"]));

        StringAssert.Contains(exception.Message, "client_secrets.json");
        StringAssert.Contains(exception.Message, "not found");
    }

    [TestMethod]
    public void Loading_throws_when_the_protected_data_is_damaged()
    {
        using var folder = new TempFolder();
        var plain = folder["client_secrets.json"];
        File.WriteAllText(plain + ClientSecretsFile.ProtectedSuffix, "not protected at all");

        var exception = Assert.ThrowsExactly<UtuException>(() => ClientSecretsFile.Load(plain));

        StringAssert.Contains(exception.Message, "cannot be decrypted");
        StringAssert.Contains(exception.Message, "client_secrets.json");
    }

    [TestMethod]
    public void Loading_throws_when_the_protected_data_is_not_a_client_secrets_document()
    {
        using var folder = new TempFolder();
        var plain = folder.WriteFile("client_secrets.json", "{}");

        var exception = Assert.ThrowsExactly<UtuException>(() => ClientSecretsFile.Load(plain));

        StringAssert.Contains(exception.Message, "client secrets");
    }

    [TestMethod]
    public void Protecting_a_missing_file_reports_the_protected_path()
    {
        using var folder = new TempFolder();
        var plain = folder["client_secrets.json"];

        Assert.AreEqual(plain + ClientSecretsFile.ProtectedSuffix, ClientSecretsFile.EnsureProtected(plain));
        Assert.IsFalse(File.Exists(plain));
        Assert.IsFalse(File.Exists(plain + ClientSecretsFile.ProtectedSuffix));
    }
}