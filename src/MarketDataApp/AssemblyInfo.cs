// Certifies that the public API is usable from every .NET language (C#, F#, VB.NET, …).
// A .NET assembly is inherently multi-language; marking it CLS-compliant makes the C#
// compiler enforce that promise — any non-CLS-compliant public member fails the build
// (which, with TreatWarningsAsErrors, is a hard gate rather than a warning).
[assembly: System.CLSCompliant(true)]
