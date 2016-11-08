# csharp-gc-helper


This module allows developers to "register" unmanaged objects represented by \<THandleType\> type. 
Typically this type will be IntPtr, but will work well if underlying library that use 32 or 64 bits handles also.

Usage is simple. Call Register passing a "class" identifier, your unmanaged object handle, a destroy handle delegate and a list of parent objects represented with the same type of the main handle.

When you are done using an object, typically on the Dispose() or Finalizer method, simply call Unregister() method, it will handle decreasing refCount of parents and free what needs to be freed.

In case you have parents that are found "lazily" later on the lifecycle of the object, you can can call AddParent()
to add the new dependency discovered.

If a dependency goes away, call RemoveParent()

Remarks: This library is entirely thread-safe not imposing serialization on the callers at all. Only assumption is that caller
uses the library properly on its calls to Unregister() through explicit calls to Dispose() or usage of using() clause
Circular parents are not detected. If you have circular parent relationships, until at least one parent is removed you will be causing a memory leak. This is no different than with other reference counted strategies.

The library ask for the concept of a handle "Class" in order to reduce contention on the registration algorithm. If you look at its implementation you will notice a retry loop at its core. 
If a concurrent unregistration is detected that results on the destruction of the object while another object is being registered with the same handle, the register method will retry the operation. You may ask, when can this happen?
This can happen right after the handle being destroyed IS destroyed, another thread calling a native API may get the memory reused, but we didn't remove yet the reference from our collection of live handles, that's when register method 
must retry the operation until it succeeds inserting the new reference in the handle tracking container.