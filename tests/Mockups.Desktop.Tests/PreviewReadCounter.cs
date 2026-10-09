using System.Reflection;
using System.Runtime.ExceptionServices;

// Test-only port decorator: production evaluation never receives this capability.
public class PreviewReadCounter : DispatchProxy
{
    private object _target = null!;
    private Dictionary<string, int> _calls = null!;

    public static T Wrap<T>(T target, Dictionary<string, int> calls) where T : class
    {
        var proxy = Create<T, PreviewReadCounter>();
        var counter = (PreviewReadCounter)(object)proxy;
        counter._target = target;
        counter._calls = calls;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var operation = method ?? throw new InvalidOperationException("Missing intercepted method.");
        var key = $"{operation.DeclaringType!.Name}.{operation.Name}";
        _calls[key] = _calls.GetValueOrDefault(key) + 1;
        try { return operation.Invoke(_target, args); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }
}
