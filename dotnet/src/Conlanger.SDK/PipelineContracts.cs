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

public sealed record InflectedWord(
    IReadOnlyList<string> Phones,
    string Lemma,
    string Pos,
    IReadOnlyList<Feat>? Features = null,
    string? Spelling = null,
    bool IsIrregular = false
);

public sealed record RealizedWordContext(
    IReadOnlyList<string> SurfacePhones,
    string Lemma,
    string Pos,
    IReadOnlyList<Feat>? Features = null,
    string? Spelling = null,
    bool IsIrregular = false
);

public interface IMorphologyRealizer
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    IReadOnlyList<IReadOnlyList<string>> RealizeWords(SyntaxTree tree, LanguageProject project);
    string RealizeForPreview(string lemma, string pos, IReadOnlyList<Feat> features, LanguageProject project);

    IReadOnlyList<InflectedWord> RealizeDetailed(SyntaxTree tree, LanguageProject project)
    {
        var leaves = tree.Leaves().Where(l => !string.IsNullOrWhiteSpace(l.Lemma)).ToList();
        var words = RealizeWords(tree, project);
        var result = new List<InflectedWord>(words.Count);
        for (int i = 0; i < words.Count; i++)
        {
            var leaf = i < leaves.Count ? leaves[i] : null;
            result.Add(new InflectedWord(words[i], leaf?.Lemma ?? "", leaf?.Category ?? "N", leaf?.Features));
        }
        return result;
    }
}

public interface IOrthographyRenderer
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    OrthographyResult Render(IReadOnlyList<IReadOnlyList<string>> surfaceWords, LanguageProject project);

    OrthographyResult RenderWords(IReadOnlyList<RealizedWordContext> words, LanguageProject project) =>
        Render(words.Select(w => w.SurfacePhones).ToList(), project);
}

public interface IPipelineExtensionProvider
{
    IReadOnlyList<ISyntaxGenerator> GetSyntaxGenerators();
    IReadOnlyList<IMorphologyRealizer> GetMorphologyRealizers();
    IReadOnlyList<IOrthographyRenderer> GetOrthographyRenderers();
    IReadOnlyList<IPhonoRule> GetPhonoRules();
}
