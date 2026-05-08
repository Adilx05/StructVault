namespace StructVault.Application.Abstractions.Persistence;

public interface IVaultSchemaProvider
{
    string GetCreateSchemaScript();
}
