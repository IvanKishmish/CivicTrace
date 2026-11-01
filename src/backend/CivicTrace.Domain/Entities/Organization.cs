using CivicTrace.Domain.Common;
using CivicTrace.Domain.Entities.Models;
using ErrorOr;

namespace CivicTrace.Domain.Entities;

/// <summary>
/// Юридична особа: замовник, виконавець або учасник. Унікальна за кодом ЄДРПОУ (унікальність забезпечує БД).
/// Фізичні особи (10-значний код) свідомо не допускаються.
/// </summary>
public sealed class Organization : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Edrpou { get; private set; } = string.Empty;
    public string? Address { get; private set; }

    private Organization()
    { } // ef

    private Organization(Guid id, OrganizationCreationParams args) : base(id)
    {
        Name = args.Name.Trim();
        Edrpou = args.Edrpou.Trim();
        Address = string.IsNullOrWhiteSpace(args.Address) ? null : args.Address.Trim();
    }

    public static ErrorOr<Organization> Create(OrganizationCreationParams args)
    {
        var validationResult = ValidateInvariants(args.Name, args.Edrpou);

        if (validationResult.IsError)
            return validationResult.Errors;

        return new Organization(Guid.CreateVersion7(), args);
    }

    public ErrorOr<Updated> UpdateDetails(string name, string? address)
    {
        var validationResult = ValidateInvariants(name, Edrpou);

        if (validationResult.IsError)
            return validationResult.Errors;

        Name = name.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

        return Result.Updated;
    }

    private static ErrorOr<Success> ValidateInvariants(string name, string edrpou)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add(Error.Validation("Organization.NameRequired", "Organization name is required."));

        var code = edrpou?.Trim() ?? string.Empty;

        if (code.Length != 8 || !code.All(char.IsAsciiDigit))
            errors.Add(Error.Validation(
                "Organization.InvalidEdrpou",
                "EDRPOU must consist of exactly 8 digits. Individuals (10-digit codes) are not stored."));

        return errors.Count > 0 ? errors : Result.Success;
    }
}