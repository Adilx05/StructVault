using System.Data.Common;
using StructVault.Application.Abstractions.Messaging;

namespace StructVault.Application.Persistence;

public sealed class CreateInMemoryVaultDatabaseCommand : ICommand<DbConnection>
{
}
