namespace Conlanger.SDK;

public sealed record SentenceSpec(
    string ClauseType,
    int ApproxWords,
    IReadOnlyDictionary<string, string>? Constraints = null
);

public sealed record SyntaxNode(
    string Category,
    string? Lemma = null,
    IReadOnlyList<Feat>? Features = null,
    IReadOnlyList<SyntaxNode>? Children = null
);

public sealed record SyntaxTree(SyntaxNode Root)
{
    public IEnumerable<SyntaxNode> Leaves()
    {
        IEnumerable<SyntaxNode> Walk(SyntaxNode node)
        {
            if (node.Children is null || node.Children.Count == 0)
            {
                yield return node;
                yield break;
            }
            foreach (var child in node.Children)
            {
                foreach (var leaf in Walk(child))
                    yield return leaf;
            }
        }
        return Walk(Root);
    }
}

public interface ISyntaxGenerator
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    SyntaxTree Generate(SentenceSpec spec, LanguageProject project, Random rng);
}

public interface IPhonoRule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool Enabled { get; }
    IReadOnlyList<string> Apply(
        IReadOnlyList<string> wordPhones,
        LanguageProject project,
        IReadOnlySet<int>? stressedIndices = null
    );
}

public interface IMorphologyRealizer
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    IReadOnlyList<IReadOnlyList<string>> RealizeWords(SyntaxTree tree, LanguageProject project);
    string RealizeForPreview(string lemma, string pos, IReadOnlyList<Feat> features, LanguageProject project);
}

public interface IOrthographyRenderer
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    OrthographyResult Render(IReadOnlyList<IReadOnlyList<string>> surfaceWords, LanguageProject project);
}

public interface IPipelineExtensionProvider
{
    IReadOnlyList<ISyntaxGenerator> GetSyntaxGenerators();
    IReadOnlyList<IMorphologyRealizer> GetMorphologyRealizers();
    IReadOnlyList<IOrthographyRenderer> GetOrthographyRenderers();
    IReadOnlyList<IPhonoRule> GetPhonoRules();
}
