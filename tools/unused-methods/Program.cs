using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: unused-methods <project.csproj> <mono-reference-directory>");
    return 2;
}

string project = Path.GetFullPath(args[0]);
string root = Path.GetDirectoryName(project)!;
var sourceFiles = XDocument.Load(project).Descendants()
    .Where(e => e.Name.LocalName == "Compile")
    .Select(e => e.Attribute("Include")?.Value.Replace('\\', '/'))
    .Where(path => path != null)
    .Select(path => Path.GetFullPath(Path.Combine(root, path!)))
    .Distinct(StringComparer.Ordinal)
    .ToArray();
var parse = new CSharpParseOptions(LanguageVersion.CSharp7_3, preprocessorSymbols: new[] { "TRACE", "PORTABLE" });
var trees = sourceFiles.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parse, path)).ToArray();
var references = Directory.GetFiles(args[1], "*.dll")
    .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
var compilation = CSharpCompilation.Create("RogueUnusedAudit", trees, references,
    new CSharpCompilationOptions(OutputKind.WindowsApplication));
var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
Console.WriteLine($"Sources: {trees.Length}; compilation errors: {errors.Length}");
foreach (var error in errors.Take(10)) Console.Error.WriteLine(error);
if (errors.Length != 0) return 1;

var methods = new Dictionary<IMethodSymbol, MethodDeclarationSyntax>(SymbolEqualityComparer.Default);
var used = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
foreach (SyntaxTree tree in trees)
{
    var model = compilation.GetSemanticModel(tree);
    var rootNode = tree.GetRoot();
    foreach (MethodDeclarationSyntax node in rootNode.DescendantNodes().OfType<MethodDeclarationSyntax>())
    {
        var symbol = model.GetDeclaredSymbol(node);
        if (symbol != null && symbol.DeclaredAccessibility == Accessibility.Private &&
            !symbol.IsOverride && !symbol.IsExtern && symbol.ExplicitInterfaceImplementations.Length == 0 &&
            node.AttributeLists.Count == 0)
            methods.Add(symbol.OriginalDefinition, node);
    }
    foreach (SyntaxNode node in rootNode.DescendantNodes().Where(n => n is IdentifierNameSyntax || n is GenericNameSyntax))
    {
        SymbolInfo info = model.GetSymbolInfo(node);
        if (info.Symbol is IMethodSymbol method) used.Add(method.OriginalDefinition);
        foreach (ISymbol candidate in info.CandidateSymbols)
            if (candidate is IMethodSymbol possible) used.Add(possible.OriginalDefinition);
    }
}

Console.WriteLine($"Private methods: {methods.Count}; without static references: {methods.Keys.Count(m => !used.Contains(m))}");
foreach (var pair in methods.Where(pair => !used.Contains(pair.Key))
    .OrderBy(pair => pair.Value.SyntaxTree.FilePath, StringComparer.Ordinal)
    .ThenBy(pair => pair.Value.GetLocation().GetLineSpan().StartLinePosition.Line))
{
    var location = pair.Value.GetLocation().GetLineSpan();
    Console.WriteLine($"{Path.GetRelativePath(root, location.Path)}:{location.StartLinePosition.Line + 1}: {pair.Key.ToDisplayString()}");
}
return 0;
