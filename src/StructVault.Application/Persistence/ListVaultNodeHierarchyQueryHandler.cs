using MediatR;
using StructVault.Application.Abstractions.Persistence;

namespace StructVault.Application.Persistence;

public sealed class ListVaultNodeHierarchyQueryHandler : IRequestHandler<ListVaultNodeHierarchyQuery, IReadOnlyList<VaultNodeHierarchyRecord>>
{
    private static readonly IComparer<VaultNodeRecord> NodeOrderComparer = Comparer<VaultNodeRecord>.Create(CompareNodes);

    private readonly IVaultNodeReader nodeReader;

    public ListVaultNodeHierarchyQueryHandler(IVaultNodeReader nodeReader)
    {
        this.nodeReader = nodeReader ?? throw new ArgumentNullException(nameof(nodeReader));
    }

    public async Task<IReadOnlyList<VaultNodeHierarchyRecord>> Handle(
        ListVaultNodeHierarchyQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<VaultNodeRecord> nodes = await nodeReader
            .ListAsync(request.Connection, new ListVaultNodesQuery(request.Connection), cancellationToken)
            .ConfigureAwait(false);

        return BuildHierarchy(nodes);
    }

    private static IReadOnlyList<VaultNodeHierarchyRecord> BuildHierarchy(IReadOnlyList<VaultNodeRecord> nodes)
    {
        if (nodes.Count == 0)
        {
            return Array.Empty<VaultNodeHierarchyRecord>();
        }

        Dictionary<string, VaultNodeRecord> nodesById = new(StringComparer.Ordinal);
        foreach (VaultNodeRecord node in nodes)
        {
            if (!nodesById.TryAdd(node.Id, node))
            {
                throw new InvalidOperationException($"Vault node hierarchy contains duplicate node id '{node.Id}'.");
            }
        }

        Dictionary<string, List<VaultNodeRecord>> childrenByParentId = new(StringComparer.Ordinal);
        List<VaultNodeRecord> rootNodes = new();
        foreach (VaultNodeRecord node in nodes)
        {
            if (node.ParentNodeId is null)
            {
                rootNodes.Add(node);
                continue;
            }

            if (string.Equals(node.ParentNodeId, node.Id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Vault node '{node.Id}' cannot be its own parent.");
            }

            if (!nodesById.ContainsKey(node.ParentNodeId))
            {
                throw new InvalidOperationException($"Vault node '{node.Id}' references missing parent node '{node.ParentNodeId}'.");
            }

            if (!childrenByParentId.TryGetValue(node.ParentNodeId, out List<VaultNodeRecord>? siblings))
            {
                siblings = new List<VaultNodeRecord>();
                childrenByParentId.Add(node.ParentNodeId, siblings);
            }

            siblings.Add(node);
        }

        foreach (List<VaultNodeRecord> siblings in childrenByParentId.Values)
        {
            siblings.Sort(NodeOrderComparer);
        }

        rootNodes.Sort(NodeOrderComparer);
        ValidateAcyclic(nodes, childrenByParentId);

        return Array.AsReadOnly(rootNodes.Select(root => BuildHierarchyNode(root, childrenByParentId)).ToArray());
    }

    private static VaultNodeHierarchyRecord BuildHierarchyNode(
        VaultNodeRecord node,
        IReadOnlyDictionary<string, List<VaultNodeRecord>> childrenByParentId)
    {
        IReadOnlyList<VaultNodeHierarchyRecord> children = childrenByParentId.TryGetValue(node.Id, out List<VaultNodeRecord>? childNodes)
            ? Array.AsReadOnly(childNodes.Select(child => BuildHierarchyNode(child, childrenByParentId)).ToArray())
            : Array.Empty<VaultNodeHierarchyRecord>();

        return new VaultNodeHierarchyRecord(
            node.Id,
            node.ParentNodeId,
            node.Name,
            node.SortOrder,
            node.CreatedAtUtc,
            node.UpdatedAtUtc,
            children);
    }

    private static void ValidateAcyclic(
        IReadOnlyList<VaultNodeRecord> nodes,
        IReadOnlyDictionary<string, List<VaultNodeRecord>> childrenByParentId)
    {
        HashSet<string> visitingNodeIds = new(StringComparer.Ordinal);
        HashSet<string> visitedNodeIds = new(StringComparer.Ordinal);

        foreach (VaultNodeRecord node in nodes)
        {
            VisitNode(node, childrenByParentId, visitingNodeIds, visitedNodeIds);
        }
    }

    private static void VisitNode(
        VaultNodeRecord node,
        IReadOnlyDictionary<string, List<VaultNodeRecord>> childrenByParentId,
        HashSet<string> visitingNodeIds,
        HashSet<string> visitedNodeIds)
    {
        if (visitedNodeIds.Contains(node.Id))
        {
            return;
        }

        if (!visitingNodeIds.Add(node.Id))
        {
            throw new InvalidOperationException($"Vault node hierarchy contains a cycle involving node '{node.Id}'.");
        }

        if (childrenByParentId.TryGetValue(node.Id, out List<VaultNodeRecord>? children))
        {
            foreach (VaultNodeRecord child in children)
            {
                VisitNode(child, childrenByParentId, visitingNodeIds, visitedNodeIds);
            }
        }

        visitingNodeIds.Remove(node.Id);
        visitedNodeIds.Add(node.Id);
    }

    private static int CompareNodes(VaultNodeRecord? left, VaultNodeRecord? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        int sortOrderComparison = left.SortOrder.CompareTo(right.SortOrder);
        if (sortOrderComparison != 0)
        {
            return sortOrderComparison;
        }

        int nameComparison = string.Compare(left.Name, right.Name, StringComparison.Ordinal);
        if (nameComparison != 0)
        {
            return nameComparison;
        }

        return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
    }
}
