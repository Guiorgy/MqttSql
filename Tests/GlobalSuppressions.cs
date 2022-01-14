using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "RCS1090:Add call to 'ConfigureAwait' (or vice versa).")]
[assembly: SuppressMessage("Security", "SCS0005:Weak random number generator.", Justification = "There is no need for a strong RNG.")]
