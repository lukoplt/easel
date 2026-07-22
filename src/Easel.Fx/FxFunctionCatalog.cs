namespace Easel.Fx;

/// <summary>
/// Curated catalog of built-in Power Fx / Power Apps functions with argument-count
/// bounds. Used for unknown-function and wrong-argument-count checks. Bounds are
/// intentionally conservative: a function is only listed with tight bounds when the
/// documented signature is unambiguous; everything else gets a generous variadic max
/// so the arity check can never produce a false error.
/// </summary>
public static class FxFunctionCatalog
{
    public const int Variadic = int.MaxValue;

    /// <summary>name → (MinArgs, MaxArgs). Case-insensitive.</summary>
    private static readonly Dictionary<string, (int Min, int Max)> Arity = new(StringComparer.OrdinalIgnoreCase)
    {
        // Logic / control flow
        ["If"] = (2, Variadic), ["Switch"] = (3, Variadic), ["And"] = (1, Variadic),
        ["Or"] = (1, Variadic), ["Not"] = (1, 1), ["Coalesce"] = (1, Variadic),
        ["IfError"] = (2, Variadic), ["IsError"] = (1, 1), ["Error"] = (1, 1),
        ["IsBlank"] = (1, 1), ["IsBlankOrError"] = (1, 1), ["IsEmpty"] = (1, 1),
        ["IsNumeric"] = (1, 1), ["IsToday"] = (1, 1), ["Blank"] = (0, 0),
        ["Sequence"] = (1, 3), ["With"] = (2, 2), ["IsMatch"] = (2, 3),

        // Tables and records
        ["Filter"] = (2, Variadic), ["LookUp"] = (2, 3), ["Search"] = (3, Variadic),
        ["Sort"] = (2, 3), ["SortByColumns"] = (2, Variadic), ["First"] = (1, 1),
        ["Last"] = (1, 1), ["FirstN"] = (1, 2), ["LastN"] = (1, 2), ["Index"] = (2, 2),
        ["CountRows"] = (1, 1), ["CountIf"] = (2, Variadic), ["Count"] = (1, 1),
        ["CountA"] = (1, 1), ["Distinct"] = (2, 2), ["GroupBy"] = (3, Variadic),
        ["Ungroup"] = (2, 2), ["AddColumns"] = (3, Variadic), ["DropColumns"] = (2, Variadic),
        ["ShowColumns"] = (2, Variadic), ["RenameColumns"] = (3, Variadic),
        ["ForAll"] = (2, 2), ["Concat"] = (2, 3), ["Table"] = (0, Variadic),
        ["Shuffle"] = (1, 1), ["Split"] = (2, 2), ["MatchAll"] = (2, 3),
        ["Match"] = (2, 3), ["AsType"] = (2, 2), ["IsType"] = (2, 2),

        // Data operations
        ["Patch"] = (2, Variadic), ["Collect"] = (2, Variadic), ["ClearCollect"] = (2, Variadic),
        ["Clear"] = (1, 1), ["Remove"] = (2, Variadic), ["RemoveIf"] = (2, Variadic),
        ["Update"] = (3, 4), ["UpdateIf"] = (3, Variadic), ["Defaults"] = (1, 1),
        ["Refresh"] = (1, 1), ["Relate"] = (2, 2), ["Unrelate"] = (2, 2),
        ["Choices"] = (1, 2), ["DataSourceInfo"] = (2, 3), ["Errors"] = (1, 2),
        ["Revert"] = (1, 2), ["Validate"] = (2, 3),

        // Variables / behavior
        ["Set"] = (2, 2), ["UpdateContext"] = (1, 1), ["Concurrent"] = (2, Variadic),
        ["Navigate"] = (1, 3), ["Back"] = (0, 1), ["Notify"] = (1, 3),
        ["Reset"] = (1, 1), ["SetFocus"] = (1, 1), ["Select"] = (1, 3),
        ["Launch"] = (1, Variadic), ["Param"] = (1, 1), ["Exit"] = (0, 1),
        ["Language"] = (0, 0), ["Download"] = (1, 1), ["SaveData"] = (2, 2),
        ["LoadData"] = (2, 3), ["ClearData"] = (0, 1), ["Trace"] = (1, 4),
        ["SubmitForm"] = (1, 1), ["ResetForm"] = (1, 1), ["NewForm"] = (1, 1),
        ["EditForm"] = (1, 1), ["ViewForm"] = (1, 1), ["RequestHide"] = (0, 0),
        ["ScanBarcode"] = (0, 1),

        // Text
        ["Text"] = (1, 3), ["Value"] = (1, 2), ["Concatenate"] = (1, Variadic),
        ["Left"] = (2, 2), ["Right"] = (2, 2), ["Mid"] = (2, 3), ["Len"] = (1, 1),
        ["Lower"] = (1, 1), ["Upper"] = (1, 1), ["Proper"] = (1, 1), ["Trim"] = (1, 1),
        ["TrimEnds"] = (1, 1), ["Substitute"] = (3, 4), ["Replace"] = (4, 4),
        ["Find"] = (2, 3), ["StartsWith"] = (2, 2), ["EndsWith"] = (2, 2),
        ["Char"] = (1, 1), ["UniChar"] = (1, 1), ["EncodeUrl"] = (1, 1),
        ["EncodeHTML"] = (1, 1), ["PlainText"] = (1, 1), ["HashTags"] = (1, 1),
        ["GUID"] = (0, 1), ["JSON"] = (1, 2), ["ParseJSON"] = (1, 2),
        ["Boolean"] = (1, 1), ["Float"] = (1, 1), ["Decimal"] = (1, 1),

        // Math
        ["Abs"] = (1, 1), ["Int"] = (1, 1), ["Trunc"] = (1, 2), ["Round"] = (2, 2),
        ["RoundUp"] = (2, 2), ["RoundDown"] = (2, 2), ["Mod"] = (2, 2),
        ["Power"] = (2, 2), ["Sqrt"] = (1, 1), ["Exp"] = (1, 1), ["Ln"] = (1, 1),
        ["Log"] = (1, 2), ["Pi"] = (0, 0), ["Rand"] = (0, 0), ["RandBetween"] = (2, 2),
        ["Sin"] = (1, 1), ["Cos"] = (1, 1), ["Tan"] = (1, 1), ["Cot"] = (1, 1),
        ["Asin"] = (1, 1), ["Acos"] = (1, 1), ["Atan"] = (1, 1), ["Atan2"] = (2, 2),
        ["Acot"] = (1, 1), ["Degrees"] = (1, 1), ["Radians"] = (1, 1),
        ["Sum"] = (1, Variadic), ["Average"] = (1, Variadic), ["Min"] = (1, Variadic),
        ["Max"] = (1, Variadic), ["StdevP"] = (1, Variadic), ["VarP"] = (1, Variadic),

        // Date / time
        ["Today"] = (0, 0), ["Now"] = (0, 0), ["UTCNow"] = (0, 0), ["UTCToday"] = (0, 0),
        ["Date"] = (3, 3), ["Time"] = (3, 4), ["DateTime"] = (3, 7),
        ["DateValue"] = (1, 2), ["TimeValue"] = (1, 2), ["DateTimeValue"] = (1, 2),
        ["DateAdd"] = (2, 3), ["DateDiff"] = (2, 3), ["Day"] = (1, 1), ["Month"] = (1, 1),
        ["Year"] = (1, 1), ["Hour"] = (1, 1), ["Minute"] = (1, 1), ["Second"] = (1, 1),
        ["Weekday"] = (1, 2), ["WeekNum"] = (1, 2), ["ISOWeekNum"] = (1, 1),
        ["EDate"] = (2, 2), ["EOMonth"] = (2, 2), ["TimeZoneOffset"] = (0, 1),

        // Color / UI
        ["RGBA"] = (4, 4), ["ColorValue"] = (1, 1), ["ColorFade"] = (2, 2),
        ["User"] = (0, 0), ["PDF"] = (1, 2),
    };

    /// <summary>True when <paramref name="name"/> is a known built-in function.</summary>
    public static bool IsKnown(string name) => Arity.ContainsKey(name);

    /// <summary>Argument bounds for a known function, or null.</summary>
    public static (int Min, int Max)? ArityOf(string name) =>
        Arity.TryGetValue(name, out var a) ? a : null;

    /// <summary>
    /// Built-in non-function identifiers and enum namespaces that appear as bare
    /// first names in formulas and must never count as "unknown".
    /// </summary>
    public static readonly HashSet<string> BuiltinIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "ThisItem", "ThisRecord", "Self", "Parent", "App", "Host", "Environment",
        // Enum namespaces (X in X.Member)
        "ScreenTransition", "Color", "Icon", "Font", "FontWeight", "DisplayMode",
        "Align", "VerticalAlign", "BorderStyle", "FormMode", "SortOrder", "TextMode",
        "TextRole", "Layout", "LayoutDirection", "LayoutMode", "LayoutOverflow",
        "AlignInContainer", "LayoutAlignItems", "LayoutJustifyContent", "LayoutWrap",
        "ImagePosition", "ImageRotation", "Transition", "RemoveFlags", "TimeUnit",
        "StartOfWeek", "DateTimeFormat", "NotificationType", "Live", "TextPosition",
        "ListItemTemplate", "BarcodeType", "GridStyle", "LabelPosition", "Direction",
        "Overflow", "PenMode", "SelectedState", "TableOverflow", "ErrorKind",
        "JSONFormat", "MatchOptions", "TraceSeverity", "ScreenSize", "Zoom",
        "DataSourceInfo", "RecordInfo", "Match", "Trigger", "OperatingSystem", "Browser",
        "TeamsTheme", "Themes", "PDFOrientation", "LoadingSpinner", "FocusedBorder",
        "SelectedText", "Acceleration", "Compass", "Connection", "Location",
    };
}
