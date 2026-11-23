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
    SyntaxTree Generate(SentenceSpec spec, LanguageProject project, Random rng);
}

public interface IPhonoRule
{
    string Name { get; }
    bool Enabled { get; }
    IReadOnlyList<string> Apply(IReadOnlyList<string> wordPhones, LanguageProject project);
}

public interface IMorphologyRealizer
{
    IReadOnlyList<IReadOnlyList<string>> RealizeWords(SyntaxTree tree, LanguageProject project);
}

public interface IOrthographyRenderer
{
    OrthographyResult Render(IReadOnlyList<IReadOnlyList<string>> surfaceWords, LanguageProject project);
}
