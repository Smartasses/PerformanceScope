# PerformanceScope
Usage:
```csharp
    PerformanceScope scope;
    using (var wrapper = PerformanceScope.Create("RootScope", request.Method.Method, request.RequestUri))
    {
      scope = wrapper.Scope;
      // execute code
      using (PerformanceScope.Create("ChildScope", request.Method.Method, request.RequestUri))
      {
          // execute code
      }
    }
    // scope contains all performance info
```