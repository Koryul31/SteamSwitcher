using SteamSwitcher.Core.Models;
using SteamSwitcher.Core.Services;
using SteamSwitcher.Core.Vdf;
using Xunit;

namespace SteamSwitcher.Core.Tests;

public class SteamAccountServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SteamInstallation _installation;

    public SteamAccountServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SteamSwitcherTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "config"));
        _installation = new SteamInstallation { DirectoryPath = _tempDir };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void GetAccounts_ReturnsParsedAndSortedAccounts()
    {
        var vdfContent = """"
        "users"
        {
        	"111"
        	{
        		"AccountName" "older_user"
        		"PersonaName" "OldPlayer"
        		"RememberPassword" "1"
        		"MostRecent" "0"
        		"Timestamp" "1000"
        	}
        	"222"
        	{
        		"AccountName" "recent_user"
        		"PersonaName" "RecentPlayer"
        		"RememberPassword" "1"
        		"MostRecent" "1"
        		"Timestamp" "2000"
        	}
        }
        """";

        File.WriteAllText(_installation.LoginUsersPath, vdfContent);

        var service = new SteamAccountService();
        var accounts = service.GetAccounts(_installation);

        Assert.Equal(2, accounts.Count);
        Assert.Equal("recent_user", accounts[0].AccountName);
        Assert.True(accounts[0].MostRecent);
        Assert.Equal("older_user", accounts[1].AccountName);
        Assert.False(accounts[1].MostRecent);
    }

    [Fact]
    public void SetActiveAccount_UpdatesMostRecentFlagsAtomically()
    {
        var vdfContent = """"
        "users"
        {
        	"111"
        	{
        		"AccountName" "user_a"
        		"MostRecent" "1"
        	}
        	"222"
        	{
        		"AccountName" "user_b"
        		"MostRecent" "0"
        	}
        }
        """";

        File.WriteAllText(_installation.LoginUsersPath, vdfContent);

        var service = new SteamAccountService();
        var result = service.SetActiveAccount(_installation, "user_b");

        Assert.True(result);

        var updatedAccounts = service.GetAccounts(_installation);
        var userB = updatedAccounts.First(a => a.AccountName == "user_b");
        var userA = updatedAccounts.First(a => a.AccountName == "user_a");

        Assert.True(userB.MostRecent);
        Assert.False(userA.MostRecent);
    }
}
