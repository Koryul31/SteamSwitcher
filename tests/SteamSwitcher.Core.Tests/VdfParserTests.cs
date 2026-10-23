using SteamSwitcher.Core.Vdf;
using Xunit;

namespace SteamSwitcher.Core.Tests;

public class VdfParserTests
{
    private const string SampleLoginUsersVdf = """"
    "users"
    {
    	"76561198000000001"
    	{
    		"AccountName"		"player_one"
    		"PersonaName"		"ProGamer2026"
    		"RememberPassword"		"1"
    		"MostRecent"		"1"
    		"Timestamp"		"1700000000"
    	}
    	"76561198000000002"
    	{
    		"AccountName"		"alt_account"
    		"PersonaName"		"SmurfKing"
    		"RememberPassword"		"1"
    		"MostRecent"		"0"
    		"Timestamp"		"1690000000"
    	}
    }
    """";

    [Fact]
    public void Parse_ValidLoginUsers_ExtractsAccountsCorrectly()
    {
        var root = VdfParser.Parse(SampleLoginUsersVdf);
        var users = root.GetObject("users");

        Assert.NotNull(users);
        Assert.Equal(2, users.Children.Count);

        var firstUser = users.GetObject("76561198000000001");
        Assert.NotNull(firstUser);
        Assert.Equal("player_one", firstUser.GetString("AccountName"));
        Assert.Equal("ProGamer2026", firstUser.GetString("PersonaName"));
        Assert.Equal("1", firstUser.GetString("RememberPassword"));
        Assert.Equal("1", firstUser.GetString("MostRecent"));

        var secondUser = users.GetObject("76561198000000002");
        Assert.NotNull(secondUser);
        Assert.Equal("alt_account", secondUser.GetString("AccountName"));
        Assert.Equal("SmurfKing", secondUser.GetString("PersonaName"));
        Assert.Equal("0", secondUser.GetString("MostRecent"));
    }

    [Fact]
    public void ModifyAndSerialize_SwapsMostRecentCorrectly()
    {
        var root = VdfParser.Parse(SampleLoginUsersVdf);
        var users = root.GetObject("users")!;

        var firstUser = users.GetObject("76561198000000001")!;
        var secondUser = users.GetObject("76561198000000002")!;

        firstUser.SetValue("MostRecent", "0");
        secondUser.SetValue("MostRecent", "1");

        var serialized = VdfSerializer.Serialize(root);
        var reparsed = VdfParser.Parse(serialized);
        var reparsedUsers = reparsed.GetObject("users")!;

        Assert.Equal("0", reparsedUsers.GetObject("76561198000000001")!.GetString("MostRecent"));
        Assert.Equal("1", reparsedUsers.GetObject("76561198000000002")!.GetString("MostRecent"));
    }

    [Fact]
    public void Parse_HandlesCommentsAndEscapedStrings()
    {
        var sample = """"
        // This is a comment
        "rootKey"
        {
            "keyWith\"Quote" "valueWith\nNewline"
        }
        """";

        var root = VdfParser.Parse(sample);
        var obj = root.GetObject("rootKey");
        Assert.NotNull(obj);
        Assert.Equal("valueWith\nNewline", obj.GetString("keyWith\"Quote"));
    }
}
