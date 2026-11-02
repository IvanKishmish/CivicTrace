namespace CivicTrace.Persistence;

internal static class ConfigurationKeys
{
    public const string DbConnectionString = "DB_CONNECTION_STRING";

    // Початковий адміністратор (необов'язково): змінні середовища Seed__Admin__Email / Seed__Admin__Password
    public const string SeedAdminEmail = "Seed:Admin:Email";
    public const string SeedAdminPassword = "Seed:Admin:Password";
}