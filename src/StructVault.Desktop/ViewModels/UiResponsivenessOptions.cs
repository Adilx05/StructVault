namespace StructVault.Desktop.ViewModels;

public sealed class UiResponsivenessOptions
{
    public const int DefaultCollectionUpdateYieldBatchSize = 100;

    public UiResponsivenessOptions(int collectionUpdateYieldBatchSize = DefaultCollectionUpdateYieldBatchSize)
    {
        if (collectionUpdateYieldBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(collectionUpdateYieldBatchSize),
                collectionUpdateYieldBatchSize,
                "Collection update yield batch size must be greater than zero.");
        }

        CollectionUpdateYieldBatchSize = collectionUpdateYieldBatchSize;
    }

    public int CollectionUpdateYieldBatchSize { get; }
}
