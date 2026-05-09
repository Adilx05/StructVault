namespace StructVault.Persistence.Schema;

public static class VaultSchema
{
    public const string VaultNodeTableName = "VaultNode";
    public const string VaultFieldTableName = "VaultField";
    public const string VaultSettingTableName = "VaultSetting";

    public const string CreateScript = """
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS VaultNode (
            Id TEXT NOT NULL PRIMARY KEY,
            ParentNodeId TEXT NULL,
            Name TEXT NOT NULL CHECK (length(trim(Name)) > 0),
            SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
            CreatedAtUtc TEXT NOT NULL CHECK (length(trim(CreatedAtUtc)) > 0),
            UpdatedAtUtc TEXT NOT NULL CHECK (length(trim(UpdatedAtUtc)) > 0),
            CONSTRAINT FK_VaultNode_ParentNode FOREIGN KEY (ParentNodeId)
                REFERENCES VaultNode(Id) ON DELETE CASCADE,
            CONSTRAINT CK_VaultNode_NoSelfParent CHECK (ParentNodeId IS NULL OR ParentNodeId <> Id)
        );

        CREATE INDEX IF NOT EXISTS IX_VaultNode_ParentNodeId_SortOrder
            ON VaultNode (ParentNodeId, SortOrder, Name, Id);

        CREATE TABLE IF NOT EXISTS VaultField (
            Id TEXT NOT NULL PRIMARY KEY,
            NodeId TEXT NOT NULL,
            Key TEXT NOT NULL CHECK (length(trim(Key)) > 0),
            Value BLOB NOT NULL,
            SortOrder INTEGER NOT NULL CHECK (SortOrder >= 0),
            CreatedAtUtc TEXT NOT NULL CHECK (length(trim(CreatedAtUtc)) > 0),
            UpdatedAtUtc TEXT NOT NULL CHECK (length(trim(UpdatedAtUtc)) > 0),
            CONSTRAINT FK_VaultField_VaultNode FOREIGN KEY (NodeId)
                REFERENCES VaultNode(Id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS IX_VaultField_NodeId_SortOrder
            ON VaultField (NodeId, SortOrder, Id);

        CREATE TABLE IF NOT EXISTS VaultSetting (
            Name TEXT NOT NULL PRIMARY KEY CHECK (length(trim(Name)) > 0),
            Value TEXT NOT NULL CHECK (length(trim(Value)) > 0),
            UpdatedAtUtc TEXT NOT NULL CHECK (length(trim(UpdatedAtUtc)) > 0)
        );
        """;
}
