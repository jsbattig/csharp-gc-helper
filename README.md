# csharp-gc-helper


This module allows developers to "register" unmanaged objects represented by <THandleType> type. 
Typically this type will be IntPtr, but will work well if underlying library uses 32 or 64 bits handles also.
Usage is simple. Call Register passing your unmanaged object handle, a destroy handle delegate and a list of parent objects represented with the same type of the main handle.

When you are done using an object, typically on the Dispose() or Finalizer method, simply call Unregister() method, it will handle decreasing refCount of parents and free what needs to be freed.

In case you have dependencies that are found "lazily" later on the lifecycle of the object, you can can call AddDependency()
to add the new dependency discovered.

If a dependency goes away, call RemoveDependency()

Remarks: This library is entirely thread-safe not imposing serialization on the callers at all. Only assumption is that caller
         uses the library properly on its calls to Unregister() through explicit calls to Dispose() or usage of using() clause
         
         Circular dependencies are not detected. If you have circular dependencies, until at least one dependency if broken
         you will be causing a memory leak. This is no different than with other reference counted strategies.

