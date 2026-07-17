using Easel.Core.Model;
using YamlDotNet.RepresentationModel;

namespace Easel.Core.Loader;

/// <summary>
/// Maps loaded pa.yaml documents into the immutable <see cref="AppModel"/>.
/// Tolerant of unknown keys (per schema-evolution risk): only recognised sections
/// are interpreted, everything else is ignored rather than rejected.
/// </summary>
public static class AppModelBuilder
{
    public static AppModel Build(YamlLoadResult load)
    {
        AppInfo? app = null;
        var screens = new List<Screen>();
        var components = new List<ComponentDefinition>();

        foreach (var doc in load.Docs)
        {
            var file = doc.RelativePath;

            if (doc.Root.Child("App") is YamlMappingNode appNode)
                app = BuildApp(appNode, doc.Root, file);

            if (doc.Root.Child("Screens") is YamlMappingNode screensNode)
                foreach (var (name, value, loc) in screensNode.Entries(file))
                    if (value is YamlMappingNode sm)
                        screens.Add(BuildScreen(name, sm, loc, file));

            if (doc.Root.Child("ComponentDefinitions") is YamlMappingNode compNode)
                foreach (var (name, value, loc) in compNode.Entries(file))
                    if (value is YamlMappingNode cm)
                        components.Add(BuildComponent(name, cm, loc, file));
        }

        app ??= new AppInfo("App", Array.Empty<Property>(), Array.Empty<NamedFormula>(), SourceLocation.Unknown);

        var media = MediaScanner.Scan(load.RootPath);
        var dataSources = DataSourceScanner.Scan(load.RootPath);

        return new AppModel(app, screens, components, dataSources, media, load.RootPath);
    }

    private static AppInfo BuildApp(YamlMappingNode appNode, YamlMappingNode docRoot, string file)
    {
        var props = ReadProperties(appNode.Child("Properties").AsMap(), file);

        var formulas = new List<NamedFormula>();
        // Named formulas may live under App.Formulas or at the document root.
        foreach (var container in new[] { appNode.Child("Formulas").AsMap(), docRoot.Child("Formulas").AsMap() })
        {
            if (container is null) continue;
            foreach (var (name, value, loc) in container.Entries(file))
            {
                var p = YamlSource.ToProperty(name, value, file);
                formulas.Add(new NamedFormula(name, p.Formula, loc));
            }
        }

        return new AppInfo("App", props, formulas, appNode.Location(file));
    }

    private static Screen BuildScreen(string name, YamlMappingNode node, SourceLocation loc, string file)
    {
        var props = ReadProperties(node.Child("Properties").AsMap(), file);
        var children = ReadChildren(node.Child("Children").AsSeq(), file);
        return new Screen(name, props, children, loc);
    }

    private static ComponentDefinition BuildComponent(string name, YamlMappingNode node, SourceLocation loc, string file)
    {
        var props = ReadProperties(node.Child("Properties").AsMap(), file);
        var children = ReadChildren(node.Child("Children").AsSeq(), file);
        return new ComponentDefinition(name, props, children, loc);
    }

    private static IReadOnlyList<Control> ReadChildren(YamlSequenceNode? seq, string file)
    {
        if (seq is null) return Array.Empty<Control>();
        var controls = new List<Control>();
        foreach (var item in seq.Children)
        {
            // Each child is a single-key mapping: { ControlName: { Control:..., Properties:... } }
            if (item is not YamlMappingNode m) continue;
            foreach (var (name, value, loc) in m.Entries(file))
                if (value is YamlMappingNode body)
                    controls.Add(BuildControl(name, body, loc, file));
        }
        return controls;
    }

    private static Control BuildControl(string name, YamlMappingNode body, SourceLocation loc, string file)
    {
        var (type, version) = SplitControlType(body.Child("Control").ScalarValue());
        var variant = body.Child("Variant").ScalarValue();
        var props = ReadProperties(body.Child("Properties").AsMap(), file);
        var children = ReadChildren(body.Child("Children").AsSeq(), file);
        return new Control(name, type, version, variant, props, children, loc);
    }

    private static IReadOnlyList<Property> ReadProperties(YamlMappingNode? map, string file)
    {
        if (map is null) return Array.Empty<Property>();
        var list = new List<Property>();
        foreach (var (name, value, _) in map.Entries(file))
            list.Add(YamlSource.ToProperty(name, value, file));
        return list;
    }

    /// <summary>Split "Classic/Button@2.2.0" into ("Classic/Button", "2.2.0").</summary>
    internal static (string Type, string? Version) SplitControlType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ("", null);
        var at = raw.LastIndexOf('@');
        return at < 0 ? (raw.Trim(), null) : (raw[..at].Trim(), raw[(at + 1)..].Trim());
    }
}
