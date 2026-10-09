using System.Reflection;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

// Test-only port decorator: production evaluation never receives this capability.
public class PreviewReadCounter : DispatchProxy
{
    private object _target = null!;
    private Dictionary<string, int> _calls = null!;
    private Dictionary<string, double>? _milliseconds;

    public static T Wrap<T>(T target, Dictionary<string, int> calls,
        Dictionary<string, double>? milliseconds = null) where T : class
    {
        var proxy = Create<T, PreviewReadCounter>();
        var counter = (PreviewReadCounter)(object)proxy;
        counter._target = target;
        counter._calls = calls;
        counter._milliseconds = milliseconds;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var operation = method ?? throw new InvalidOperationException("Missing intercepted method.");
        var key = $"{operation.DeclaringType!.Name}.{operation.Name}";
        _calls[key] = _calls.GetValueOrDefault(key) + 1;
        var watch = Stopwatch.StartNew();
        try { return operation.Invoke(_target, args); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
        finally
        {
            if (_milliseconds is not null)
                _milliseconds[key] = _milliseconds.GetValueOrDefault(key) + watch.Elapsed.TotalMilliseconds;
        }
    }
}
