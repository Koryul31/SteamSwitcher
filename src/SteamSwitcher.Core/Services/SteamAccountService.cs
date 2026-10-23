using System.Text;
using SteamSwitcher.Core.Models;
using SteamSwitcher.Core.Vdf;

namespace SteamSwitcher.Core.Services;

public interface ISteamAccountService
{
    IReadOnlyList<SteamAccount> GetAccounts(SteamInstallation installation);
    bool SetActiveAccount(SteamInstallation installation, string accountName);
}

public sealed class SteamAccountService : ISteamAccountService
{
    public IReadOnlyList<SteamAccount> GetAccounts(SteamInstallation installation)
    {
        if (!File.Exists(installation.LoginUsersPath))
        {
            return [];
        }

        try
        {
            string content;
            using (var stream = new FileStream(
                       installation.LoginUsersPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }

            var root = VdfParser.Parse(content);
            var usersObj = root.GetObject("users") ?? root;
            var accounts = new List<SteamAccount>();

            foreach (var child in usersObj.Children)
            {
                if (child is not VdfObject userObj) continue;

                var steamId64 = userObj.Key;
                var accountName = userObj.GetString("AccountName");
                if (string.IsNullOrWhiteSpace(accountName)) continue;

                var personaName = userObj.GetString("PersonaName") ?? string.Empty;
                var remember = userObj.GetString("RememberPassword") == "1";
                var mostRecent = userObj.GetString("MostRecent") == "1";
                long.TryParse(userObj.GetString("Timestamp"), out var timestamp);

                var avatar = FindAvatar(installation.AvatarCacheDirectory, steamId64);

                accounts.Add(new SteamAccount
                {
                    SteamId64 = steamId64,
                    AccountName = accountName,
                    PersonaName = personaName,
                    RememberPassword = remember,
                    MostRecent = mostRecent,
                    Timestamp = timestamp,
                    AvatarPath = avatar
                });
            }

            return accounts
                .OrderByDescending(a => a.MostRecent)
                .ThenByDescending(a => a.Timestamp)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public bool SetActiveAccount(SteamInstallation installation, string accountName)
    {
        if (!File.Exists(installation.LoginUsersPath))
        {
            return false;
        }

        try
        {
            string content;
            using (var stream = new FileStream(
                       installation.LoginUsersPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }

            var root = VdfParser.Parse(content);
            var usersObj = root.GetObject("users") ?? root;
            var found = false;

            foreach (var child in usersObj.Children)
            {
                if (child is not VdfObject userObj) continue;

                var currentAccount = userObj.GetString("AccountName");
                if (string.Equals(currentAccount, accountName, StringComparison.OrdinalIgnoreCase))
                {
                    userObj.SetValue("MostRecent", "1");
                    found = true;
                }
                else
                {
                    userObj.SetValue("MostRecent", "0");
                }
            }

            if (!found)
            {
                return false;
            }

            var serialized = VdfSerializer.Serialize(root);
            var tempPath = installation.LoginUsersPath + ".tmp";
            File.WriteAllText(tempPath, serialized, new UTF8Encoding(false));

            if (File.Exists(installation.LoginUsersPath))
            {
                File.Replace(tempPath, installation.LoginUsersPath, null);
            }
            else
            {
                File.Move(tempPath, installation.LoginUsersPath);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindAvatar(string avatarCacheDir, string steamId64)
    {
        if (!Directory.Exists(avatarCacheDir)) return null;

        var extensions = new[] { ".png", ".jpg", ".jpeg" };
        foreach (var ext in extensions)
        {
            var candidate = Path.Combine(avatarCacheDir, steamId64 + ext);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
