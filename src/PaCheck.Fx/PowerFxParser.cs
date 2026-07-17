using Microsoft.PowerFx;
using Microsoft.PowerFx.Syntax;

namespace PaCheck.Fx;

/// <summary>
/// Thin, host-independent wrapper over the Microsoft.PowerFx parser.
/// Parse only (no binding) — pacheck analyses source, it does not evaluate.
/// </summary>
public sealed class PowerFxParser
{
    private readonly Engine _engine;
    private readonly ParserOptions _options;

    public PowerFxParser()
    {
        // Empty config: we never bind against a symbol table, we only need the AST.
        _engine = new Engine(new PowerFxConfig());
        _options = new ParserOptions
        {
            // Power Apps behaviour properties use ';' chained expressions.
            AllowsSideEffects = true,
        };
    }

    /// <summary>Parse a single Power Fx expression into an AST. Never throws on invalid input.</summary>
    public FxParse Parse(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new FxParse(expression ?? string.Empty, null, true, Array.Empty<FxDiagnostic>());
        }

        try
        {
            ParseResult result = _engine.Parse(expression, _options);
            var errors = result.Errors
                .Select(e => new FxDiagnostic(
                    e.Message,
                    (int)(e.Span.Min),
                    (int)(e.Span.Lim)))
                .ToArray();

            return new FxParse(expression, result.Root, result.IsSuccess, errors);
        }
        catch (Exception ex)
        {
            // Tolerant mode: a parser blow-up must never crash analysis (PF0001 upstream).
            return new FxParse(expression, null, false,
                new[] { new FxDiagnostic($"parser exception: {ex.Message}", 0, 0) });
        }
    }
}

/// <summary>Result of parsing one expression.</summary>
public sealed record FxParse(
    string Expression,
    TexlNode? Root,
    bool IsSuccess,
    IReadOnlyList<FxDiagnostic> Errors);

/// <summary>A parse-level diagnostic with a character span into the expression.</summary>
public sealed record FxDiagnostic(string Message, int SpanStart, int SpanEnd);
